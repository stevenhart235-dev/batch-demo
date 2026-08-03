using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;

namespace BatchDemo.Application;

public sealed class BatchIntakeService(
    IOriginalObjectStore objectStore,
    IBatchRepository repository,
    TimeProvider timeProvider)
{
    public async Task<BatchIntakeResult> IntakeAsync(
        string? merchantId,
        string? rawFilename,
        Stream? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            throw new IntakeValidationException("merchantId", "A merchantId is required.");
        }

        if (content is null)
        {
            throw new IntakeValidationException("file", "A CSV file is required.");
        }

        var batchId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var filename = ArtifactKeyFactory.SanitizeFilename(rawFilename);
        var objectKey = ArtifactKeyFactory.Original(merchantId, batchId, filename);
        StoredOriginal stored;

        try
        {
            stored = await objectStore.StoreAsync(
                objectKey,
                content,
                "text/csv; charset=utf-8",
                cancellationToken);
        }
        catch
        {
            throw;
        }

        try
        {
            var candidate = Batch.CreateReceived(
                batchId,
                merchantId.Trim(),
                filename,
                stored.ObjectKey,
                stored.Sha256,
                now);

            var persisted = await repository.PersistIntakeAsync(candidate, cancellationToken);
            return BatchIntakeResult.From(persisted);
        }
        catch (Exception persistenceError)
        {
            try
            {
                await objectStore.DeleteIfExistsAsync(objectKey, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                throw new IntakeCompensationException(persistenceError, cleanupError);
            }

            throw;
        }
    }
}

public sealed record BatchIntakeResult(
    Guid BatchId,
    string MerchantId,
    string OriginalFilename,
    string OriginalSha256,
    BatchStatus Status,
    Guid? CanonicalBatchId,
    DateTimeOffset ReceivedAt,
    int? AcceptedCount,
    int? RejectedCount,
    int? TotalRowCount,
    string? AcceptedArtifactKey,
    string? RejectedArtifactKey,
    string? SummaryArtifactKey,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? ProcessingCompletedAt)
{
    public static BatchIntakeResult From(Batch batch) => new(
        batch.BatchId,
        batch.MerchantId,
        batch.OriginalFilename,
        batch.OriginalSha256,
        batch.Status,
        batch.CanonicalBatchId,
        batch.ReceivedAt,
        batch.AcceptedCount, batch.RejectedCount, batch.TotalRowCount,
        batch.AcceptedArtifactKey, batch.RejectedArtifactKey, batch.SummaryArtifactKey,
        batch.ProcessingStartedAt, batch.ProcessingCompletedAt);
}

public sealed class IntakeValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class IntakeCompensationException(Exception persistenceError, Exception cleanupError)
    : AggregateException(
        "Database persistence failed and the preserved object could not be removed; reconciliation is required.",
        persistenceError,
        cleanupError)
{
}
