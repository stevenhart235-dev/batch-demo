using BatchDemo.Application;
using BatchDemo.Domain;

namespace BatchDemo.Api.Contracts;

public sealed record BatchResponse(
    Guid BatchId,
    string MerchantId,
    string OriginalFilename,
    string OriginalSha256,
    BatchStatus Status,
    Guid? CanonicalBatchId,
    DateTimeOffset ReceivedAt,
    string StatusUrl,
    int? AcceptedCount,
    int? RejectedCount,
    int? TotalRowCount,
    string? AcceptedArtifactKey,
    string? RejectedArtifactKey,
    string? SummaryArtifactKey,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? ProcessingCompletedAt)
{
    public static BatchResponse From(BatchIntakeResult result, string statusUrl) => new(
        result.BatchId,
        result.MerchantId,
        result.OriginalFilename,
        result.OriginalSha256,
        result.Status,
        result.CanonicalBatchId,
        result.ReceivedAt,
        statusUrl,
        result.AcceptedCount, result.RejectedCount, result.TotalRowCount,
        result.AcceptedArtifactKey, result.RejectedArtifactKey, result.SummaryArtifactKey,
        result.ProcessingStartedAt, result.ProcessingCompletedAt);
}
