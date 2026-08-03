using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BatchDemo.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BatchDemoDbContext>
{
    public BatchDemoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BATCHDEMO_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set BATCHDEMO_CONNECTION_STRING before creating or applying migrations.");
        }

        var options = new DbContextOptionsBuilder<BatchDemoDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new BatchDemoDbContext(options);
    }
}
