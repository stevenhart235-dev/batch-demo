using BatchDemo.Application;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BatchDemo.Infrastructure.Persistence;

public sealed class EfBatchRepository(BatchDemoDbContext dbContext, TimeProvider timeProvider) : IBatchRepository
{
    private const string CanonicalConstraint = "ux_batches_canonical_delivery";

    public async Task<Batch> PersistIntakeAsync(Batch candidate, CancellationToken cancellationToken)
    {
        var canonical = await FindCanonicalAsync(candidate.MerchantId, candidate.OriginalSha256, cancellationToken);
        if (canonical is not null)
        {
            DuplicateClassifier.Apply(candidate, canonical.BatchId, timeProvider.GetUtcNow());
            dbContext.Batches.Add(candidate);
            await dbContext.SaveChangesAsync(cancellationToken);
            return candidate;
        }

        candidate.AddPendingWorkItem(Guid.NewGuid(), timeProvider.GetUtcNow());
        dbContext.Batches.Add(candidate);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return candidate;
        }
        catch (DbUpdateException exception) when (IsCanonicalConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            canonical = await FindCanonicalAsync(candidate.MerchantId, candidate.OriginalSha256, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The canonical delivery conflict was reported, but the canonical batch could not be loaded.",
                    exception);

            var duplicate = Batch.CreateReceived(
                candidate.BatchId,
                candidate.MerchantId,
                candidate.OriginalFilename,
                candidate.OriginalObjectKey,
                candidate.OriginalSha256,
                candidate.ReceivedAt);
            DuplicateClassifier.Apply(duplicate, canonical.BatchId, timeProvider.GetUtcNow());
            dbContext.Batches.Add(duplicate);
            await dbContext.SaveChangesAsync(cancellationToken);
            return duplicate;
        }
    }

    public Task<Batch?> FindAsync(Guid batchId, CancellationToken cancellationToken)
    {
        return dbContext.Batches.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batchId, cancellationToken);
    }

    private Task<Batch?> FindCanonicalAsync(string merchantId, string sha256, CancellationToken cancellationToken)
    {
        return dbContext.Batches
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId && x.OriginalSha256 == sha256 && x.CanonicalBatchId == null)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool IsCanonicalConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CanonicalConstraint
        };
    }
}
