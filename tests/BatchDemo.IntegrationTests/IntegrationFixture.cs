using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BatchDemo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BatchDemo.IntegrationTests;

public sealed class IntegrationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ConnectionString = "Host=127.0.0.1;Port=55432;Database=batch_demo;Username=batch_demo;Password=batch_demo_local";
    public const string BucketName = "batch-demo";
    public const string S3Url = "http://127.0.0.1:9000";
    public const string AccessKey = "batch_demo_local";
    public const string SecretKey = "batch_demo_local_secret";

    public IntegrationFixture()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__BatchDemo", ConnectionString);
        Environment.SetEnvironmentVariable("S3__ServiceUrl", S3Url);
        Environment.SetEnvironmentVariable("S3__AccessKey", AccessKey);
        Environment.SetEnvironmentVariable("S3__SecretKey", SecretKey);
        Environment.SetEnvironmentVariable("S3__BucketName", BucketName);
        Environment.SetEnvironmentVariable("S3__Region", "us-east-1");
    }

    public IAmazonS3 S3Client { get; } = new AmazonS3Client(
        new BasicAWSCredentials(AccessKey, SecretKey),
        new AmazonS3Config
        {
            ServiceURL = S3Url,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:BatchDemo"] = ConnectionString,
                ["S3:ServiceUrl"] = S3Url,
                ["S3:AccessKey"] = AccessKey,
                ["S3:SecretKey"] = SecretKey,
                ["S3:BucketName"] = BucketName,
                ["S3:Region"] = "us-east-1"
            }));
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await EnsureBucketAsync();
        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BatchDemoDbContext>();
        await database.Database.MigrateAsync();
        await database.Database.ExecuteSqlRawAsync("TRUNCATE TABLE batch_work_items, batches CASCADE");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        S3Client.Dispose();
        await base.DisposeAsync();
    }

    public async Task<BatchDemoDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<BatchDemoDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var database = new BatchDemoDbContext(options);
        await database.Database.OpenConnectionAsync();
        return database;
    }

    private async Task EnsureBucketAsync()
    {
        try
        {
            await S3Client.GetBucketAclAsync(new GetBucketAclRequest { BucketName = BucketName });
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await S3Client.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });
        }
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    public const string Name = "Integration";
}
