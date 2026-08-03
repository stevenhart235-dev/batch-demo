using BatchDemo.Application;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;

namespace BatchDemo.UnitTests;

public sealed class BatchIntakeValidationTests
{
    [Fact]
    public async Task Missing_merchant_id_is_rejected()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<IntakeValidationException>(() =>
            service.IntakeAsync(" ", "batch.csv", new MemoryStream(), CancellationToken.None));

        Assert.Equal("merchantId", exception.Field);
    }

    [Fact]
    public async Task Missing_file_is_rejected()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<IntakeValidationException>(() =>
            service.IntakeAsync("merchant_demo", null, null, CancellationToken.None));

        Assert.Equal("file", exception.Field);
    }

    private static BatchIntakeService CreateService() => new(
        new UnexpectedObjectStore(),
        new UnexpectedRepository(),
        TimeProvider.System);

    private sealed class UnexpectedObjectStore : IOriginalObjectStore
    {
        public Task<StoredOriginal> StoreAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Validation should occur before storage.");

        public Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Validation should occur before storage.");
        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task PublishUtf8IfAbsentAsync(string objectKey, string content, string contentType, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class UnexpectedRepository : IBatchRepository
    {
        public Task<Batch> PersistIntakeAsync(Batch candidate, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Validation should occur before persistence.");

        public Task<Batch?> FindAsync(Guid batchId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Validation should occur before persistence.");
    }
}
