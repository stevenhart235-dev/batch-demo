using System.ComponentModel.DataAnnotations;

namespace BatchDemo.Worker;

public sealed class ProcessingWorkerOptions
{
    [Range(1, 3600)] public int PollingIntervalSeconds { get; init; } = 2;
    [Range(5, 3600)] public int LeaseDurationSeconds { get; init; } = 60;
    [Range(1, 20)] public int MaximumAttempts { get; init; } = 3;
}
