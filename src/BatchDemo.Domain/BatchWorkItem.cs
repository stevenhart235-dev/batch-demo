namespace BatchDemo.Domain;

public sealed class BatchWorkItem
{
    private BatchWorkItem()
    {
    }

    public Guid WorkItemId { get; private set; }
    public Guid BatchId { get; private set; }
    public string WorkType { get; private set; } = string.Empty;
    public WorkItemStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Batch Batch { get; private set; } = null!;

    internal static BatchWorkItem CreatePending(
        Guid workItemId,
        Guid batchId,
        string workType,
        DateTimeOffset now)
    {
        return new BatchWorkItem
        {
            WorkItemId = workItemId,
            BatchId = batchId,
            WorkType = workType,
            Status = WorkItemStatus.Pending,
            AvailableAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Lease(string owner, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Status = WorkItemStatus.Leased; LeaseOwner = owner; LeaseExpiresAt = expiresAt;
        AttemptCount++; LastError = null; UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = WorkItemStatus.Completed; LeaseOwner = null; LeaseExpiresAt = null; UpdatedAt = now;
    }

    public void ReleaseOrFail(string error, int maximumAttempts, DateTimeOffset now)
    {
        LastError = error; LeaseOwner = null; LeaseExpiresAt = null; UpdatedAt = now;
        Status = AttemptCount >= maximumAttempts ? WorkItemStatus.Failed : WorkItemStatus.Pending;
        AvailableAt = now;
    }
}
