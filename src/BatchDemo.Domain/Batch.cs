namespace BatchDemo.Domain;

public sealed class Batch
{
    private Batch()
    {
    }

    public Guid BatchId { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string OriginalFilename { get; private set; } = string.Empty;
    public string OriginalObjectKey { get; private set; } = string.Empty;
    public string OriginalSha256 { get; private set; } = string.Empty;
    public BatchStatus Status { get; private set; }
    public Guid? CanonicalBatchId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int? AcceptedCount { get; private set; }
    public int? RejectedCount { get; private set; }
    public int? TotalRowCount { get; private set; }
    public string? AcceptedArtifactKey { get; private set; }
    public string? RejectedArtifactKey { get; private set; }
    public string? SummaryArtifactKey { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? ProcessingCompletedAt { get; private set; }
    public Batch? CanonicalBatch { get; private set; }
    public IReadOnlyCollection<BatchWorkItem> WorkItems => _workItems;

    private readonly List<BatchWorkItem> _workItems = [];

    public static Batch CreateReceived(
        Guid batchId,
        string merchantId,
        string originalFilename,
        string originalObjectKey,
        string originalSha256,
        DateTimeOffset now)
    {
        return new Batch
        {
            BatchId = batchId,
            MerchantId = merchantId,
            OriginalFilename = originalFilename,
            OriginalObjectKey = originalObjectKey,
            OriginalSha256 = originalSha256,
            Status = BatchStatus.Received,
            ReceivedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkDuplicate(Guid canonicalBatchId, DateTimeOffset now)
    {
        Status = BatchStatus.Duplicate;
        CanonicalBatchId = canonicalBatchId;
        UpdatedAt = now;
    }

    public BatchWorkItem AddPendingWorkItem(Guid workItemId, DateTimeOffset now)
    {
        if (Status != BatchStatus.Received)
        {
            throw new InvalidOperationException("Only received batches can be queued.");
        }

        var item = BatchWorkItem.CreatePending(workItemId, BatchId, "ValidateAndNormalize", now);
        _workItems.Add(item);
        return item;
    }

    public void MarkProcessingStarted(DateTimeOffset now)
    {
        ProcessingStartedAt ??= now;
        UpdatedAt = now;
    }

    public void CompleteProcessing(int total, int accepted, int rejected, string acceptedKey,
        string rejectedKey, string summaryKey, BatchStatus status, DateTimeOffset now)
    {
        TotalRowCount = total; AcceptedCount = accepted; RejectedCount = rejected;
        AcceptedArtifactKey = acceptedKey; RejectedArtifactKey = rejectedKey; SummaryArtifactKey = summaryKey;
        Status = status; ProcessingCompletedAt = now; UpdatedAt = now;
    }

    public void MarkProcessingFailed(DateTimeOffset now)
    {
        Status = BatchStatus.ProcessingFailed; ProcessingCompletedAt = now; UpdatedAt = now;
    }
}
