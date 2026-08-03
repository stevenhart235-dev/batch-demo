using System.ComponentModel.DataAnnotations;

namespace BatchDemo.Infrastructure.ObjectStorage;

public sealed class S3Options
{
    public const string SectionName = "S3";

    [Required]
    public required string ServiceUrl { get; init; }
    [Required]
    public required string AccessKey { get; init; }
    [Required]
    public required string SecretKey { get; init; }
    [Required]
    public required string BucketName { get; init; }
    [Required]
    public string Region { get; init; } = "us-east-1";
}
