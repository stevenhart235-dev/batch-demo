using System.Text;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;
using BatchDemo.Infrastructure.Csv;

namespace BatchDemo.UnitTests;

public sealed class CsvBatchFileParserTests
{
    [Fact]
    public async Task Handles_quotes_metadata_normalization_and_source_rows()
    {
        const string csv = "merchant_reference,operation,amount,currency,payment_credential_reference,original_authorization_reference,requested_execution_date,note\n" +
            "REF-1,Purchase,12.30,usd,tok_one,,,\"hello, \"\"world\"\"\"\n" +
            "REF-2,Refund,1.00,EUR,tok_two,auth_two,2026-08-04,\"multi\nline\"\n";
        var result = await Parse(csv);
        Assert.Equal(BatchStatus.Ready, result.Status); Assert.Equal(2, result.Accepted.Count);
        Assert.Equal(1230, result.Accepted[0].AmountMinor); Assert.Equal("USD", result.Accepted[0].Currency);
        Assert.Equal("hello, \"world\"", result.Accepted[0].SourceMetadata["note"]);
        Assert.Equal(2, result.Accepted[0].SourceRowNumber); Assert.Equal(3, result.Accepted[1].SourceRowNumber);
        Assert.Contains("\nline", result.Accepted[1].OriginalRowContent);
    }

    [Fact]
    public async Task Applies_row_rejection_rules_and_duplicate_scope()
    {
        const string csv = "merchant_reference,operation,amount,currency,payment_credential_reference,original_authorization_reference,requested_execution_date\n" +
            "A,Purchase,1.001,usd,tok,,,\nA,Refund,1.00,ZZZ,,,bad-date\n";
        var result = await Parse(csv);
        Assert.Equal(BatchStatus.Rejected, result.Status); Assert.Empty(result.Accepted); Assert.Equal(2, result.Rejected.Count);
        Assert.Contains(result.Rejected[0].Reasons, x => x.Code == "InvalidAmount");
        Assert.Contains(result.Rejected[1].Reasons, x => x.Code == "DuplicateMerchantReference");
        Assert.Contains(result.Rejected[1].Reasons, x => x.Code == "UnsupportedCurrency");
        Assert.Contains(result.Rejected[1].Reasons, x => x.Code == "MissingCredentialReference");
        Assert.Contains(result.Rejected[1].Reasons, x => x.Code == "InvalidOriginalAuthorizationReference");
        Assert.Contains(result.Rejected[1].Reasons, x => x.Code == "InvalidExecutionDate");
    }

    [Fact]
    public async Task Missing_required_header_is_structural_failure()
    {
        var result = await Parse("merchant_reference,amount\nA,1.00\n");
        Assert.Equal(BatchStatus.Rejected, result.Status); Assert.Empty(result.Accepted);
        Assert.Contains(result.FileRejectionReasons, x => x.Code == "MissingRequiredColumn");
    }

    private static async Task<BatchDemo.Application.ProcessingResult> Parse(string text)
    {
        var work = new ClaimedWork(Guid.NewGuid(), Guid.NewGuid(), "test", "merchant", "batch.csv", "original", new string('a', 64), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);
        return await new CsvBatchFileParser().ParseAsync(work, new MemoryStream(Encoding.UTF8.GetBytes(text)), CancellationToken.None);
    }
}
