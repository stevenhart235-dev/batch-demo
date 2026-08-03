using System.Globalization;
using System.Text;
using BatchDemo.Application;
using BatchDemo.Application.Abstractions;
using CsvHelper;
using CsvHelper.Configuration;

namespace BatchDemo.Infrastructure.Csv;

public sealed class CsvBatchFileParser : IBatchFileParser
{
    private const long MaximumBytes = 10_000_000;
    private const int MaximumRows = 10_000;
    private static readonly string[] RequiredColumns =
        ["merchant_reference", "operation", "amount", "currency", "payment_credential_reference"];
    private static readonly HashSet<string> KnownColumns =
        [.. RequiredColumns, "original_authorization_reference", "requested_execution_date"];

    public async Task<ProcessingResult> ParseAsync(ClaimedWork work, Stream original, CancellationToken cancellationToken)
    {
        if (original.CanSeek && original.Length > MaximumBytes)
            return FileFailure(work, "FileTooLarge", "File exceeds 10,000,000 bytes.");

        try
        {
            using var reader = new StreamReader(original, new UTF8Encoding(false, true), true, 16 * 1024, leaveOpen: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.None
            });
            if (!await csv.ReadAsync() || !csv.ReadHeader())
                return FileFailure(work, "MissingRequiredColumn", "A header row is required.");
            var headers = csv.HeaderRecord?.Select(x => x.Trim()).ToArray() ?? [];
            if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Length)
                return FileFailure(work, "MalformedCsv", "Header contains duplicate column names.", csv.Parser.RawRecord);
            var missing = RequiredColumns.Where(x => !headers.Contains(x, StringComparer.Ordinal)).ToArray();
            if (missing.Length > 0)
                return FileFailure(work, "MissingRequiredColumn", $"Missing required columns: {string.Join(", ", missing)}.", csv.Parser.RawRecord);

            var accepted = new List<AcceptedRecord>();
            var rejected = new List<RejectedRecord>();
            var references = new HashSet<string>(StringComparer.Ordinal);
            var previousRawRow = csv.Parser.RawRow;
            var total = 0;
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                total++;
                if (total > MaximumRows) return FileFailure(work, "TooManyRows", "File exceeds 10,000 data rows.");
                var sourceRow = previousRawRow + 1;
                previousRawRow = csv.Parser.RawRow;
                var raw = TrimRecordTerminator(csv.Parser.RawRecord);
                var values = headers.Select((header, index) => new { header, value = csv.GetField(index) })
                    .ToDictionary(x => x.header, x => x.value, StringComparer.Ordinal);
                var metadata = values.Where(x => !KnownColumns.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.Ordinal);
                var merchantReference = Get(values, "merchant_reference")?.Trim();
                var operationInput = Get(values, "operation")?.Trim();
                var amount = Get(values, "amount");
                var currency = Get(values, "currency")?.Trim().ToUpperInvariant();
                var credential = Get(values, "payment_credential_reference");
                var authorization = Get(values, "original_authorization_reference");
                var executionDate = Get(values, "requested_execution_date");
                var reasons = Validate(merchantReference, operationInput, amount, currency, credential, authorization, executionDate, references);
                if (reasons.Count > 0)
                {
                    rejected.Add(new(work.BatchId, sourceRow, merchantReference, raw, metadata, reasons, work.IngestedAt));
                    continue;
                }
                var operation = operationInput!.Equals("purchase", StringComparison.OrdinalIgnoreCase) ? "Purchase" : "Refund";
                MoneyNormalizer.TryToMinorUnits(amount, out var minor);
                accepted.Add(new(work.BatchId, sourceRow, merchantReference!, operation, minor, currency!, credential!,
                    string.IsNullOrWhiteSpace(authorization) ? null : authorization,
                    string.IsNullOrWhiteSpace(executionDate) ? null : executionDate, work.IngestedAt, raw, metadata));
            }
            return new(accepted, rejected, [], total);
        }
        catch (DecoderFallbackException) { return FileFailure(work, "InvalidEncoding", "Input is not valid UTF-8."); }
        catch (CsvHelperException) { return FileFailure(work, "MalformedCsv", "CSV structure could not be parsed."); }
    }

    private static List<RejectionReason> Validate(string? reference, string? operation, string? amount,
        string? currency, string? credential, string? authorization, string? date, HashSet<string> references)
    {
        var reasons = new List<RejectionReason>();
        if (string.IsNullOrWhiteSpace(reference)) reasons.Add(new("MissingMerchantReference", "Merchant reference is required.", "merchant_reference"));
        else if (reference.Length > 100) reasons.Add(new("MissingMerchantReference", "Merchant reference exceeds 100 characters.", "merchant_reference"));
        else if (!references.Add(reference)) reasons.Add(new("DuplicateMerchantReference", "Merchant reference duplicates an earlier row.", "merchant_reference"));
        var validOperation = operation?.Equals("Purchase", StringComparison.OrdinalIgnoreCase) == true || operation?.Equals("Refund", StringComparison.OrdinalIgnoreCase) == true;
        if (!validOperation) reasons.Add(new("InvalidOperation", "Operation must be Purchase or Refund.", "operation"));
        if (!MoneyNormalizer.TryToMinorUnits(amount, out _)) reasons.Add(new("InvalidAmount", "Amount must be positive with at most two fractional digits.", "amount"));
        if (currency is not ("USD" or "EUR" or "GBP" or "CAD")) reasons.Add(new("UnsupportedCurrency", "Currency is not supported.", "currency"));
        if (string.IsNullOrWhiteSpace(credential)) reasons.Add(new("MissingCredentialReference", "Payment credential reference is required.", "payment_credential_reference"));
        var refund = operation?.Equals("Refund", StringComparison.OrdinalIgnoreCase) == true;
        if ((refund && (string.IsNullOrWhiteSpace(authorization) || authorization.Length > 200)) ||
            (!refund && !string.IsNullOrWhiteSpace(authorization)))
            reasons.Add(new("InvalidOriginalAuthorizationReference", "Original authorization reference does not match the operation.", "original_authorization_reference"));
        if (!string.IsNullOrWhiteSpace(date) && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            reasons.Add(new("InvalidExecutionDate", "Requested execution date must be YYYY-MM-DD.", "requested_execution_date"));
        return reasons;
    }

    private static string? Get(Dictionary<string, string?> values, string key) => values.GetValueOrDefault(key);
    private static string TrimRecordTerminator(string value) => value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2] : value.EndsWith('\n') ? value[..^1] : value;
    private static ProcessingResult FileFailure(ClaimedWork work, string code, string message, string raw = "") =>
        new([], [new(work.BatchId, null, null, TrimRecordTerminator(raw), new Dictionary<string, string?>(), [new(code, message)], work.IngestedAt)], [new(code, message)], 0);
}
