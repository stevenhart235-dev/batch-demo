using BatchDemo.Application;
using BatchDemo.Domain;

namespace BatchDemo.UnitTests;

public sealed class ProcessingDecisionTests
{
    [Theory]
    [InlineData(1, 0, false, BatchStatus.Ready)]
    [InlineData(1, 1, false, BatchStatus.ReadyWithExceptions)]
    [InlineData(0, 1, false, BatchStatus.Rejected)]
    [InlineData(0, 0, true, BatchStatus.Rejected)]
    public void Calculates_final_status(int accepted, int rejected, bool structural, BatchStatus expected) =>
        Assert.Equal(expected, ProcessingStatus.Calculate(accepted, rejected, structural));

    [Fact]
    public void Retries_then_fails_at_maximum_attempts()
    {
        var now = DateTimeOffset.UtcNow;
        var batch = Batch.CreateReceived(Guid.NewGuid(), "m", "f", "k", new string('a', 64), now);
        var item = batch.AddPendingWorkItem(Guid.NewGuid(), now);
        item.Lease("one", now.AddMinutes(1), now); item.ReleaseOrFail("safe", 3, now);
        Assert.Equal(WorkItemStatus.Pending, item.Status);
        item.Lease("two", now.AddMinutes(1), now); item.ReleaseOrFail("safe", 3, now);
        item.Lease("three", now.AddMinutes(1), now); item.ReleaseOrFail("safe", 3, now);
        Assert.Equal(WorkItemStatus.Failed, item.Status); Assert.Equal(3, item.AttemptCount);
    }
}
