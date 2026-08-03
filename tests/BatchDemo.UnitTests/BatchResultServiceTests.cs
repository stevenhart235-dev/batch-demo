using System.Text;
using BatchDemo.Application;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;

namespace BatchDemo.UnitTests;

public sealed class BatchResultServiceTests
{
    [Fact]
    public async Task Projects_artifacts_without_credential_reference_value()
    {
        var batch = CompletedBatch();
        var store = new MemoryStore(new Dictionary<string, string>
        {
            ["accepted"] = """{"batchId":"00000000-0000-0000-0000-000000000001","sourceRowNumber":2,"merchantReference":"REF","operation":"Purchase","amountMinor":1234,"currency":"USD","paymentCredentialReference":"secret-token","originalAuthorizationReference":null,"requestedExecutionDate":null,"ingestedAt":"2026-08-03T00:00:00Z","originalRowContent":"row","sourceMetadata":{}}""" + "\n",
            ["rejected"] = "",
            ["summary"] = """{"ingestedAt":"2026-08-03T00:00:00Z","artifactGeneratedAt":"2026-08-03T00:00:01Z","fileRejectionReasons":[],"artifacts":{"original":"original","accepted":"accepted","rejected":"rejected","summary":"summary"}}"""
        });
        var result = await new BatchResultService(new Repository(batch), store).FindAsync(batch.BatchId, default);
        var accepted = Assert.Single(result!.Accepted);
        Assert.True(accepted.CredentialReferencePresent);
        Assert.DoesNotContain("secret-token", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal(["accepted", "rejected", "summary"], store.ReadKeys);
    }

    [Fact]
    public async Task Received_batch_is_a_controlled_conflict_without_object_access()
    {
        var batch = Batch.CreateReceived(Guid.NewGuid(), "merchant", "a.csv", "private/original", "sha", DateTimeOffset.UtcNow);
        var store = new MemoryStore(new Dictionary<string, string>());
        await Assert.ThrowsAsync<BatchResultUnavailableException>(() =>
            new BatchResultService(new Repository(batch), store).FindAsync(batch.BatchId, default));
        Assert.Empty(store.ReadKeys);
    }

    [Theory]
    [InlineData("REF,Purchase,12.00,USD,tok_secret,,", "REF,Purchase,12.00,USD,[credential redacted],,")]
    [InlineData("REF,Purchase,12.00,USD,\"tok,secret\",,", "REF,Purchase,12.00,USD,[credential redacted],,")]
    public void Rejected_row_redacts_credential_column(string row, string expected) =>
        Assert.Equal(expected, PortalRowRedactor.RedactCredential(row, 2));

    private static Batch CompletedBatch()
    {
        var batch = Batch.CreateReceived(Guid.Parse("00000000-0000-0000-0000-000000000001"), "merchant", "a.csv", "original", "sha", DateTimeOffset.UtcNow);
        batch.MarkProcessingStarted(DateTimeOffset.UtcNow);
        batch.CompleteProcessing(1, 1, 0, "accepted", "rejected", "summary", BatchStatus.Ready, DateTimeOffset.UtcNow);
        return batch;
    }

    private sealed class Repository(Batch batch) : IBatchRepository
    {
        public Task<Batch?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<Batch?>(id == batch.BatchId ? batch : null);
        public Task<Batch> PersistIntakeAsync(Batch candidate, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class MemoryStore(IReadOnlyDictionary<string, string> values) : IOriginalObjectStore
    {
        public List<string> ReadKeys { get; } = [];
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) { ReadKeys.Add(key); return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(values[key]))); }
        public Task<StoredOriginal> StoreAsync(string key, Stream content, string type, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishUtf8IfAbsentAsync(string key, string content, string type, CancellationToken ct) => throw new NotSupportedException();
    }
}
