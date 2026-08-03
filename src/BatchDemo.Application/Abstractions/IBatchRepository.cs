using BatchDemo.Domain;

namespace BatchDemo.Application.Abstractions;

public interface IBatchRepository
{
    Task<Batch> PersistIntakeAsync(Batch candidate, CancellationToken cancellationToken);
    Task<Batch?> FindAsync(Guid batchId, CancellationToken cancellationToken);
}
