using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.S3.Model;
using BatchDemo.Application;
using BatchDemo.Domain;
using BatchDemo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BatchDemo.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class BatchProcessingIntegrationTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Sample_processes_to_documented_artifacts_and_is_idempotent()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "demo-merchant-batch.csv"));
        var batchId = await Upload(await File.ReadAllBytesAsync(path));
        var work = await Claim("worker-sample"); Assert.NotNull(work);
        await Process(work!); await Process(work!);

        await using var db = await fixture.CreateDbContextAsync();
        var batch = await db.Batches.SingleAsync(x => x.BatchId == batchId);
        var item = await db.BatchWorkItems.SingleAsync(x => x.BatchId == batchId);
        Assert.Equal(BatchStatus.ReadyWithExceptions, batch.Status); Assert.Equal(6, batch.AcceptedCount);
        Assert.Equal(4, batch.RejectedCount); Assert.Equal(10, batch.TotalRowCount); Assert.Equal(WorkItemStatus.Completed, item.Status);
        var accepted = await Read(batch.AcceptedArtifactKey!); var rejected = await Read(batch.RejectedArtifactKey!); var summary = await Read(batch.SummaryArtifactKey!);
        Assert.Equal(6, JsonLines(accepted).Count); Assert.Equal(4, JsonLines(rejected).Count);
        using var summaryJson = JsonDocument.Parse(summary); Assert.Equal("ReadyWithExceptions", summaryJson.RootElement.GetProperty("status").GetString());
        Assert.False(summaryJson.RootElement.TryGetProperty("completedAt", out _));
        var artifactGeneratedAt = summaryJson.RootElement.GetProperty("artifactGeneratedAt").GetDateTimeOffset();
        Assert.Equal(batch.ProcessingStartedAt, artifactGeneratedAt);
        Assert.NotNull(batch.ProcessingCompletedAt);
        Assert.True(batch.ProcessingCompletedAt >= batch.ProcessingStartedAt);
        using var apiJson = JsonDocument.Parse(await fixture.CreateClient().GetStringAsync($"/api/batches/{batchId:D}"));
        Assert.Equal(batch.ProcessingCompletedAt, apiJson.RootElement.GetProperty("processingCompletedAt").GetDateTimeOffset());
        var portalJson = await fixture.CreateClient().GetStringAsync($"/api/batches/{batchId:D}/results");
        using var portal = JsonDocument.Parse(portalJson);
        Assert.Equal(6, portal.RootElement.GetProperty("accepted").GetArrayLength());
        Assert.Equal(4, portal.RootElement.GetProperty("rejected").GetArrayLength());
        Assert.Contains("credentialReferencePresent", portalJson);
        Assert.DoesNotContain("paymentCredentialReference", portalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tok_demo_", portalJson, StringComparison.OrdinalIgnoreCase);
        Assert.All(JsonLines(accepted), x => { Assert.True(x.RootElement.TryGetProperty("sourceRowNumber", out _)); Assert.True(x.RootElement.TryGetProperty("originalRowContent", out _)); });
        Assert.All(JsonLines(rejected), x => Assert.True(x.RootElement.GetProperty("reasons").GetArrayLength() > 0));
    }

    [Fact]
    public async Task Results_endpoint_controls_incomplete_missing_and_structural_results()
    {
        var incomplete = await Upload("merchant_reference,operation,amount,currency,payment_credential_reference\nA,Purchase,1,USD,tok\n"u8.ToArray());
        Assert.Equal(System.Net.HttpStatusCode.Conflict,
            (await fixture.CreateClient().GetAsync($"/api/batches/{incomplete:D}/results")).StatusCode);
        await Process((await Claim($"cleanup-{Guid.NewGuid():N}"))!);
        Assert.Equal(System.Net.HttpStatusCode.NotFound,
            (await fixture.CreateClient().GetAsync($"/api/batches/{Guid.NewGuid():D}/results")).StatusCode);

        var structural = await Upload("merchant_reference,amount\nA,1.00\n"u8.ToArray());
        await Process((await Claim($"structural-{Guid.NewGuid():N}"))!);
        using var result = JsonDocument.Parse(await fixture.CreateClient().GetStringAsync($"/api/batches/{structural:D}/results"));
        Assert.Equal("Rejected", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, result.RootElement.GetProperty("acceptedRows").GetInt32());
        Assert.Equal(0, result.RootElement.GetProperty("rejectedRows").GetInt32());
        Assert.True(result.RootElement.GetProperty("fileRejectionReasons").GetArrayLength() > 0);
        var rejection = result.RootElement.GetProperty("rejected")[0];
        Assert.Equal(JsonValueKind.Null, rejection.GetProperty("sourceRowNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, rejection.GetProperty("merchantReference").ValueKind);
    }

    [Theory]
    [InlineData("merchant_reference,operation,amount,currency,payment_credential_reference\nA,Purchase,1.00,USD,tok\n", BatchStatus.Ready, 1, 0)]
    [InlineData("merchant_reference,amount\nA,1.00\n", BatchStatus.Rejected, 0, 0)]
    [InlineData("merchant_reference,operation,amount,currency,payment_credential_reference\n,Nope,bad,ZZZ,\n", BatchStatus.Rejected, 0, 1)]
    public async Task Final_status_cases(string csv, BatchStatus expected, int accepted, int rejected)
    {
        var id = await Upload(Encoding.UTF8.GetBytes(csv)); var work = await Claim($"worker-{Guid.NewGuid():N}");
        await Process(work!); await using var db = await fixture.CreateDbContextAsync();
        var batch = await db.Batches.SingleAsync(x => x.BatchId == id);
        Assert.Equal(expected, batch.Status); Assert.Equal(accepted, batch.AcceptedCount); Assert.Equal(rejected, batch.RejectedCount);
        if (batch.TotalRowCount == 0)
        {
            var fileRejection = Assert.Single(JsonLines(await Read(batch.RejectedArtifactKey!))).RootElement;
            Assert.Equal(JsonValueKind.Null, fileRejection.GetProperty("sourceRowNumber").ValueKind);
            Assert.Equal(JsonValueKind.Null, fileRejection.GetProperty("merchantReference").ValueKind);
            Assert.Equal(JsonValueKind.Object, fileRejection.GetProperty("sourceMetadata").ValueKind);
            Assert.True(fileRejection.GetProperty("reasons").GetArrayLength() > 0);
            Assert.True(fileRejection.TryGetProperty("originalRowContent", out _));
            Assert.True(fileRejection.TryGetProperty("ingestedAt", out _));
        }
    }

    [Fact]
    public async Task Concurrent_claimers_only_claim_once_and_expired_lease_is_reclaimed()
    {
        var id = await Upload("merchant_reference,operation,amount,currency,payment_credential_reference\nA,Purchase,1,USD,tok\n"u8.ToArray());
        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Claim($"concurrent-{i}")));
        var claimed = Assert.Single(claims, x => x is not null)!;
        await using (var db = await fixture.CreateDbContextAsync())
            await db.BatchWorkItems.Where(x => x.WorkItemId == claimed.WorkItemId).ExecuteUpdateAsync(x => x.SetProperty(y => y.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        var reclaimed = await Claim("reclaimer"); Assert.NotNull(reclaimed); Assert.Equal(id, reclaimed.BatchId); Assert.Equal(2, reclaimed.AttemptCount);
        await Process(reclaimed);
    }

    private async Task<Guid> Upload(byte[] bytes)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent($"process_{Guid.NewGuid():N}"), "merchantId");
        form.Add(new ByteArrayContent(bytes), "file", "batch.csv");
        var response = await fixture.CreateClient().PostAsync("/api/batches", form); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadResponse>())!.BatchId;
    }
    private async Task<BatchDemo.Application.Abstractions.ClaimedWork?> Claim(string owner)
    { await using var scope = fixture.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<BatchProcessingService>().ClaimAsync(owner, TimeSpan.FromMinutes(1), 3, CancellationToken.None); }
    private async Task Process(BatchDemo.Application.Abstractions.ClaimedWork work)
    { await using var scope = fixture.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<BatchProcessingService>().ProcessAsync(work, 3, CancellationToken.None); }
    private async Task<string> Read(string key) { using var response = await fixture.S3Client.GetObjectAsync(new GetObjectRequest { BucketName = IntegrationFixture.BucketName, Key = key }); using var reader = new StreamReader(response.ResponseStream); return await reader.ReadToEndAsync(); }
    private static List<JsonDocument> JsonLines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => JsonDocument.Parse(x)).ToList();
    private sealed record UploadResponse(Guid BatchId);
}
