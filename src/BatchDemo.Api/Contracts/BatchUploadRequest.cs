using Microsoft.AspNetCore.Mvc;

namespace BatchDemo.Api.Contracts;

public sealed class BatchUploadRequest
{
    [FromForm(Name = "merchantId")]
    public string? MerchantId { get; init; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
