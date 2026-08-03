using BatchDemo.Domain;

namespace BatchDemo.Application.Abstractions;

public interface IWorkQueue
{
    Task<ClaimedWork?> ClaimAsync(string leaseOwner, TimeSpan leaseDuration, int maximumAttempts, CancellationToken cancellationToken);
    Task CompleteAsync(ClaimedWork work, ProcessingResult result, ArtifactKeys keys, CancellationToken cancellationToken);
    Task FailAsync(ClaimedWork work, string safeError, int maximumAttempts, CancellationToken cancellationToken);
}

public sealed record ClaimedWork(Guid WorkItemId, Guid BatchId, string LeaseOwner, string MerchantId, string OriginalFilename,
    string OriginalObjectKey, string OriginalSha256, DateTimeOffset IngestedAt, DateTimeOffset ProcessingStartedAt,
    int AttemptCount);
