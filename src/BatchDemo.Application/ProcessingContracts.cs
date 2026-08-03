using System.Text.Json.Serialization;
using BatchDemo.Domain;
using BatchDemo.Application.Abstractions;

namespace BatchDemo.Application;

public sealed record ProcessingResult(IReadOnlyList<AcceptedRecord> Accepted,
    IReadOnlyList<RejectedRecord> Rejected, IReadOnlyList<RejectionReason> FileRejectionReasons, int TotalRows)
{
    [JsonIgnore] public BatchStatus Status => ProcessingStatus.Calculate(Accepted.Count, Rejected.Count, FileRejectionReasons.Count > 0);
    [JsonIgnore] public int RowRejectedCount => Rejected.Count(x => x.SourceRowNumber is not null);
}

public sealed record AcceptedRecord(Guid BatchId, int SourceRowNumber, string MerchantReference, string Operation,
    long AmountMinor, string Currency, string PaymentCredentialReference, string? OriginalAuthorizationReference,
    string? RequestedExecutionDate, DateTimeOffset IngestedAt, string OriginalRowContent,
    IReadOnlyDictionary<string, string?> SourceMetadata);

public sealed record RejectedRecord(Guid BatchId, int? SourceRowNumber, string? MerchantReference,
    string OriginalRowContent, IReadOnlyDictionary<string, string?> SourceMetadata,
    IReadOnlyList<RejectionReason> Reasons, DateTimeOffset IngestedAt);

public sealed record RejectionReason(string Code, string Message, string? Field = null);
public sealed record ArtifactKeys(string Accepted, string Rejected, string Summary);

public interface IBatchFileParser
{
    Task<ProcessingResult> ParseAsync(ClaimedWork work, Stream original, CancellationToken cancellationToken);
}

public static class ProcessingStatus
{
    public static BatchStatus Calculate(int accepted, int rejected, bool structuralFailure) =>
        structuralFailure || accepted == 0 ? BatchStatus.Rejected :
        rejected == 0 ? BatchStatus.Ready : BatchStatus.ReadyWithExceptions;
}

public static class MoneyNormalizer
{
    public static bool TryToMinorUnits(string? value, out long minorUnits)
    {
        minorUnits = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('e', StringComparison.OrdinalIgnoreCase) ||
            value.Contains(',') || value.StartsWith('+') || value.StartsWith('-')) return false;
        var parts = value.Split('.');
        if (parts.Length > 2 || parts[0].Length == 0 || parts.Any(p => p.Any(c => !char.IsAsciiDigit(c))) ||
            (parts.Length == 2 && parts[1].Length > 2)) return false;
        var normalized = parts[0] + (parts.Length == 1 ? "00" : parts[1].PadRight(2, '0'));
        return long.TryParse(normalized, out minorUnits) && minorUnits > 0;
    }
}
