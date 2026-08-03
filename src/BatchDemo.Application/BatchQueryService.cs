using BatchDemo.Application.Abstractions;

namespace BatchDemo.Application;

public sealed class BatchQueryService(IBatchRepository repository)
{
    public async Task<BatchIntakeResult?> FindAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await repository.FindAsync(batchId, cancellationToken);
        return batch is null ? null : BatchIntakeResult.From(batch);
    }
}
