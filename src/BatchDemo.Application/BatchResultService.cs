using System.Text.Json;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;

namespace BatchDemo.Application;

public sealed class BatchResultService(IBatchRepository repository, IOriginalObjectStore objects)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BatchPortalResult?> FindAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await repository.FindAsync(batchId, cancellationToken);
        if (batch is null) return null;

        if (batch.Status is BatchStatus.Received)
            throw new BatchResultUnavailableException("Batch processing is not complete.");

        if (batch.Status is BatchStatus.Duplicate or BatchStatus.ProcessingFailed)
            return BatchPortalResult.WithoutArtifacts(batch);

        if (batch.AcceptedArtifactKey is null || batch.RejectedArtifactKey is null || batch.SummaryArtifactKey is null)
            throw new BatchResultUnavailableException("The batch result artifacts are unavailable.");

        try
        {
            var accepted = await ReadJsonLinesAsync<AcceptedRecord>(batch.AcceptedArtifactKey, cancellationToken);
            var rejected = await ReadJsonLinesAsync<RejectedRecord>(batch.RejectedArtifactKey, cancellationToken);
            var summary = await ReadJsonAsync<PortalArtifactSummary>(batch.SummaryArtifactKey, cancellationToken);
            return BatchPortalResult.From(batch, accepted, rejected, summary);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BatchResultUnavailableException("The batch result artifacts could not be read.", exception);
        }
    }

    private async Task<IReadOnlyList<T>> ReadJsonLinesAsync<T>(string key, CancellationToken cancellationToken)
    {
        await using var stream = await objects.OpenReadAsync(key, cancellationToken);
        using var reader = new StreamReader(stream);
        var records = new List<T>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            records.Add(JsonSerializer.Deserialize<T>(line, JsonOptions)
                ?? throw new JsonException("Artifact record was null."));
        }
        return records;
    }

    private async Task<T> ReadJsonAsync<T>(string key, CancellationToken cancellationToken)
    {
        await using var stream = await objects.OpenReadAsync(key, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new JsonException("Artifact was null.");
    }
}

public sealed record PortalAcceptedRecord(int SourceRowNumber, string MerchantReference, string Operation,
    long AmountMinor, string Currency, string? OriginalAuthorizationReference, string? RequestedExecutionDate,
    bool CredentialReferencePresent);

public sealed record PortalRejectedRecord(int? SourceRowNumber, string? MerchantReference, string OriginalRowContent,
    IReadOnlyList<RejectionReason> Reasons);

public sealed record PortalArtifacts(string Original, string Accepted, string Rejected, string Summary);
public sealed record PortalArtifactSummary(DateTimeOffset IngestedAt, DateTimeOffset ArtifactGeneratedAt,
    IReadOnlyList<RejectionReason> FileRejectionReasons, PortalArtifacts Artifacts);

public sealed record BatchPortalResult(Guid BatchId, string MerchantId, string OriginalFilename, string OriginalSha256,
    BatchStatus Status, Guid? CanonicalBatchId, DateTimeOffset ReceivedAt, DateTimeOffset? ProcessingCompletedAt,
    int? TotalRows, int? AcceptedRows, int? RejectedRows, IReadOnlyList<PortalAcceptedRecord> Accepted,
    IReadOnlyList<PortalRejectedRecord> Rejected, DateTimeOffset? IngestedAt, DateTimeOffset? ArtifactGeneratedAt,
    PortalArtifacts? Artifacts, IReadOnlyList<RejectionReason> FileRejectionReasons)
{
    public static BatchPortalResult WithoutArtifacts(Batch batch) => new(batch.BatchId, batch.MerchantId,
        batch.OriginalFilename, batch.OriginalSha256, batch.Status, batch.CanonicalBatchId, batch.ReceivedAt,
        batch.ProcessingCompletedAt, batch.TotalRowCount, batch.AcceptedCount, batch.RejectedCount, [], [], null, null,
        null, []);

    public static BatchPortalResult From(Batch batch, IReadOnlyList<AcceptedRecord> accepted,
        IReadOnlyList<RejectedRecord> rejected, PortalArtifactSummary summary) => new(batch.BatchId, batch.MerchantId,
        batch.OriginalFilename, batch.OriginalSha256, batch.Status, batch.CanonicalBatchId, batch.ReceivedAt,
        batch.ProcessingCompletedAt, batch.TotalRowCount, batch.AcceptedCount, batch.RejectedCount,
        accepted.Select(x => new PortalAcceptedRecord(x.SourceRowNumber, x.MerchantReference, x.Operation,
            x.AmountMinor, x.Currency, x.OriginalAuthorizationReference, x.RequestedExecutionDate,
            !string.IsNullOrEmpty(x.PaymentCredentialReference))).ToList(),
        rejected.Select(x => new PortalRejectedRecord(x.SourceRowNumber, x.MerchantReference,
            PortalRowRedactor.RedactCredential(x.OriginalRowContent, x.SourceRowNumber), x.Reasons)).ToList(), summary.IngestedAt, summary.ArtifactGeneratedAt,
        summary.Artifacts, summary.FileRejectionReasons);
}

public static class PortalRowRedactor
{
    public static string RedactCredential(string row, int? sourceRowNumber)
    {
        if (sourceRowNumber is null) return "[File content retained in the protected rejection artifact]";
        var field = 0; var quoted = false; var start = 0;
        for (var i = 0; i <= row.Length; i++)
        {
            if (i < row.Length && row[i] == '"')
            {
                if (quoted && i + 1 < row.Length && row[i + 1] == '"') { i++; continue; }
                quoted = !quoted; continue;
            }
            if (i < row.Length && (row[i] != ',' || quoted)) continue;
            if (field == 4) return string.Concat(row.AsSpan(0, start), "[credential redacted]", row.AsSpan(i));
            field++; start = i + 1;
        }
        return field == 4 ? string.Concat(row.AsSpan(0, start), "[credential redacted]") : row;
    }
}

public sealed class BatchResultUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
