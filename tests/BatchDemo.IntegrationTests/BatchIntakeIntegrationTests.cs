using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3.Model;
using BatchDemo.Domain;
using Microsoft.EntityFrameworkCore;

namespace BatchDemo.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BatchIntakeIntegrationTests(IntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string SamplePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "demo-merchant-batch.csv"));

    [Fact]
    public async Task Upload_preserves_bytes_hash_work_item_and_get_metadata()
    {
        var bytes = await File.ReadAllBytesAsync(SamplePath);
        var merchantId = $"merchant_{Guid.NewGuid():N}";
        var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        var response = await UploadAsync(merchantId, bytes);

        Assert.Equal(HttpStatusCode.Accepted, response.Message.StatusCode);
        Assert.Equal($"/api/batches/{response.Body.BatchId:D}", response.Message.Headers.Location?.AbsolutePath);
        Assert.Equal(BatchStatus.Received, response.Body.Status);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), response.Body.OriginalSha256);

        using (var stored = await fixture.S3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = IntegrationFixture.BucketName,
            Key = response.Body.OriginalObjectKey
        }))
        await using (var memory = new MemoryStream())
        {
            await stored.ResponseStream.CopyToAsync(memory);
            Assert.Equal(bytes, memory.ToArray());
        }

        await using var database = await fixture.CreateDbContextAsync();
        var batch = await database.Batches.SingleAsync(x => x.BatchId == response.Body.BatchId);
        var work = await database.BatchWorkItems.SingleAsync(x => x.BatchId == response.Body.BatchId);
        Assert.Equal(response.Body.OriginalSha256, batch.OriginalSha256);
        Assert.Equal(WorkItemStatus.Pending, work.Status);
        Assert.Equal(0, work.AttemptCount);

        var get = await client.GetAsync(response.Body.StatusUrl);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var getBody = await get.Content.ReadFromJsonAsync<ApiBatchResponse>(JsonOptions);
        Assert.NotNull(getBody);
        Assert.Equal(response.Body.BatchId, getBody.BatchId);
        Assert.Equal(response.Body.MerchantId, getBody.MerchantId);
        Assert.Equal(response.Body.OriginalFilename, getBody.OriginalFilename);
        Assert.Equal(response.Body.OriginalSha256, getBody.OriginalSha256);
        Assert.Equal(response.Body.Status, getBody.Status);
    }

    [Fact]
    public async Task Same_merchant_and_bytes_creates_duplicate_without_work()
    {
        var bytes = "merchant_reference,operation\nA,Purchase\n"u8.ToArray();
        var merchantId = $"merchant_{Guid.NewGuid():N}";

        var canonical = await UploadAsync(merchantId, bytes);
        var duplicate = await UploadAsync(merchantId, bytes);

        Assert.Equal(BatchStatus.Received, canonical.Body.Status);
        Assert.Equal(BatchStatus.Duplicate, duplicate.Body.Status);
        Assert.Equal(canonical.Body.BatchId, duplicate.Body.CanonicalBatchId);
        await using var database = await fixture.CreateDbContextAsync();
        Assert.False(await database.BatchWorkItems.AnyAsync(x => x.BatchId == duplicate.Body.BatchId));
    }

    [Fact]
    public async Task Same_bytes_for_different_merchants_are_not_duplicates()
    {
        var bytes = "same exact bytes"u8.ToArray();

        var first = await UploadAsync($"merchant_{Guid.NewGuid():N}", bytes);
        var second = await UploadAsync($"merchant_{Guid.NewGuid():N}", bytes);

        Assert.Equal(BatchStatus.Received, first.Body.Status);
        Assert.Equal(BatchStatus.Received, second.Body.Status);
        Assert.Null(second.Body.CanonicalBatchId);
    }

    [Fact]
    public async Task Concurrent_identical_uploads_create_one_canonical_processing_batch()
    {
        var bytes = "concurrent identical bytes"u8.ToArray();
        var merchantId = $"merchant_{Guid.NewGuid():N}";

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UploadAsync(merchantId, bytes)));

        var canonical = Assert.Single(results, x => x.Body.Status == BatchStatus.Received);
        Assert.All(
            results.Where(x => x.Body.Status == BatchStatus.Duplicate),
            duplicate => Assert.Equal(canonical.Body.BatchId, duplicate.Body.CanonicalBatchId));
        Assert.Equal(7, results.Count(x => x.Body.Status == BatchStatus.Duplicate));

        await using var database = await fixture.CreateDbContextAsync();
        var ids = results.Select(x => x.Body.BatchId).ToArray();
        Assert.Equal(8, await database.Batches.CountAsync(x => ids.Contains(x.BatchId)));
        Assert.Equal(1, await database.BatchWorkItems.CountAsync(x => ids.Contains(x.BatchId)));
    }

    private async Task<UploadResult> UploadAsync(string merchantId, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(merchantId), "merchantId");
        content.Add(new ByteArrayContent(bytes), "file", "demo-merchant-batch.csv");
        var message = await fixture.CreateClient().PostAsync("/api/batches", content);
        var responseContent = await message.Content.ReadAsStringAsync();
        if (!message.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"The API returned {(int)message.StatusCode}: {responseContent}");
        }

        var body = JsonSerializer.Deserialize<ApiBatchResponse>(responseContent, JsonOptions)
            ?? throw new InvalidOperationException("The API returned no response body.");

        await using var database = await fixture.CreateDbContextAsync();
        body = body with
        {
            OriginalObjectKey = await database.Batches
                .Where(x => x.BatchId == body.BatchId)
                .Select(x => x.OriginalObjectKey)
                .SingleAsync()
        };
        return new UploadResult(message, body);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record UploadResult(HttpResponseMessage Message, ApiBatchResponse Body);

    private sealed record ApiBatchResponse(
        Guid BatchId,
        string MerchantId,
        string OriginalFilename,
        string OriginalSha256,
        BatchStatus Status,
        Guid? CanonicalBatchId,
        DateTimeOffset ReceivedAt,
        string StatusUrl,
        string OriginalObjectKey = "");
}
