using BatchDemo.Application;
using Microsoft.Extensions.Options;

namespace BatchDemo.Worker;

public sealed class Worker(IServiceScopeFactory scopes, IOptions<ProcessingWorkerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        logger.LogInformation("Batch worker started as {LeaseOwner}", _leaseOwner);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<BatchProcessingService>();
                var work = await service.ClaimAsync(_leaseOwner, TimeSpan.FromSeconds(settings.LeaseDurationSeconds), settings.MaximumAttempts, stoppingToken);
                if (work is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(settings.PollingIntervalSeconds), stoppingToken);
                    continue;
                }
                logger.LogInformation("Claimed batch {BatchId}, work {WorkItemId}, attempt {Attempt}, stage processing",
                    work.BatchId, work.WorkItemId, work.AttemptCount);
                await service.ProcessAsync(work, settings.MaximumAttempts, stoppingToken);
                logger.LogInformation("Finished batch {BatchId}, work {WorkItemId}, attempt {Attempt}", work.BatchId, work.WorkItemId, work.AttemptCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Worker polling failure");
                await Task.Delay(TimeSpan.FromSeconds(settings.PollingIntervalSeconds), stoppingToken);
            }
        }
        logger.LogInformation("Batch worker stopped");
    }
}
