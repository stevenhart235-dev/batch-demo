using Amazon.Runtime;
using Amazon.S3;
using BatchDemo.Application.Abstractions;
using BatchDemo.Application;
using BatchDemo.Infrastructure.Health;
using BatchDemo.Infrastructure.Csv;
using BatchDemo.Infrastructure.ObjectStorage;
using BatchDemo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BatchDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBatchDemoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BatchDemo")
            ?? throw new InvalidOperationException("ConnectionStrings:BatchDemo is required.");

        services.AddDbContext<BatchDemoDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBatchRepository, EfBatchRepository>();
        services.AddScoped<IWorkQueue, EfWorkQueue>();
        services.AddScoped<IBatchFileParser, CsvBatchFileParser>();
        services.AddScoped<BatchProcessingService>();

        services.AddOptions<S3Options>()
            .Bind(configuration.GetSection(S3Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var s3 = configuration.GetRequiredSection(S3Options.SectionName).Get<S3Options>()
            ?? throw new InvalidOperationException("S3 configuration is required.");
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(s3.AccessKey, s3.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = s3.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = s3.Region
            }));
        services.AddScoped<IOriginalObjectStore, S3OriginalObjectStore>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"])
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);
        return services;
    }
}
