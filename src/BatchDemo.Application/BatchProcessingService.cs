using System.Text.Json;
using BatchDemo.Application.Abstractions;

namespace BatchDemo.Application;

public sealed class BatchProcessingService(IWorkQueue queue, IOriginalObjectStore objects, IBatchFileParser parser)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public async Task<ClaimedWork?> ClaimAsync(string owner, TimeSpan lease, int maxAttempts, CancellationToken ct) =>
        await queue.ClaimAsync(owner, lease, maxAttempts, ct);

    public async Task ProcessAsync(ClaimedWork work, int maxAttempts, CancellationToken cancellationToken)
    {
        try
        {
            await using var original = await objects.OpenReadAsync(work.OriginalObjectKey, cancellationToken);
            var result = await parser.ParseAsync(work, original, cancellationToken);
            var keys = ProcessingArtifactKeys.For(work.MerchantId, work.BatchId);
            var accepted = ToJsonLines(result.Accepted);
            var rejected = ToJsonLines(result.Rejected);
            var summary = JsonSerializer.Serialize(new
            {
                work.BatchId,
                work.MerchantId,
                status = result.Status.ToString(),
                work.OriginalFilename,
                originalSha256 = work.OriginalSha256,
                ingestedAt = work.IngestedAt,
                artifactGeneratedAt = work.ProcessingStartedAt,
                totalRows = result.TotalRows,
                acceptedRows = result.Accepted.Count,
                rejectedRows = result.RowRejectedCount,
                fileRejectionReasons = result.FileRejectionReasons,
                artifacts = new { original = work.OriginalObjectKey, accepted = keys.Accepted, rejected = keys.Rejected, summary = keys.Summary }
            }, JsonOptions);
            await objects.PublishUtf8IfAbsentAsync(keys.Accepted, accepted, "application/x-ndjson", cancellationToken);
            await objects.PublishUtf8IfAbsentAsync(keys.Rejected, rejected, "application/x-ndjson", cancellationToken);
            await objects.PublishUtf8IfAbsentAsync(keys.Summary, summary, "application/json", cancellationToken);
            await queue.CompleteAsync(work, result, keys, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            await queue.FailAsync(work, SafeError(exception), maxAttempts, CancellationToken.None);
        }
    }

    private static string ToJsonLines<T>(IReadOnlyList<T> records) =>
        records.Count == 0 ? string.Empty : string.Join('\n', records.Select(x => JsonSerializer.Serialize(x, JsonOptions))) + "\n";
    private static string SafeError(Exception exception) => $"{exception.GetType().Name}: {exception.Message}"[..Math.Min(2000, exception.GetType().Name.Length + 2 + exception.Message.Length)];
}

public static class ProcessingArtifactKeys
{
    public static ArtifactKeys For(string merchantId, Guid batchId)
    {
        var prefix = ArtifactKeyFactory.Original(merchantId, batchId, "placeholder");
        prefix = prefix[..prefix.IndexOf("/original/", StringComparison.Ordinal)];
        return new($"{prefix}/results/accepted.jsonl", $"{prefix}/results/rejected.jsonl", $"{prefix}/results/summary.json");
    }
}
