using Amazon.S3;
using Amazon.S3.Model;
using BatchDemo.Infrastructure.ObjectStorage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BatchDemo.Infrastructure.Health;

public sealed class ObjectStorageHealthCheck(IAmazonS3 client, IOptions<S3Options> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetBucketAclAsync(
                new GetBucketAclRequest { BucketName = options.Value.BucketName },
                cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("S3-compatible object storage is unavailable.", exception);
        }
    }
}
