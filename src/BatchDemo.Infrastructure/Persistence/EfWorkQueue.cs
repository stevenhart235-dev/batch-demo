using BatchDemo.Application;
using BatchDemo.Application.Abstractions;
using BatchDemo.Domain;
using Microsoft.EntityFrameworkCore;

namespace BatchDemo.Infrastructure.Persistence;

public sealed class EfWorkQueue(BatchDemoDbContext database, TimeProvider timeProvider) : IWorkQueue
{
    public async Task<ClaimedWork?> ClaimAsync(string owner, TimeSpan leaseDuration, int maximumAttempts, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = PostgreSqlTimestamp(timeProvider.GetUtcNow());
        var exhausted = await database.BatchWorkItems.FromSqlInterpolated($"""
            SELECT * FROM batch_work_items
            WHERE status = 'Leased' AND lease_expires_at <= {now} AND attempt_count >= {maximumAttempts}
            ORDER BY lease_expires_at
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (exhausted is not null)
        {
            exhausted.ReleaseOrFail("Lease expired after maximum attempts.", maximumAttempts, now);
            (await database.Batches.SingleAsync(x => x.BatchId == exhausted.BatchId, cancellationToken)).MarkProcessingFailed(now);
            await database.SaveChangesAsync(cancellationToken);
        }
        var item = await database.BatchWorkItems.FromSqlInterpolated($"""
            SELECT * FROM batch_work_items
            WHERE ((status = 'Pending' AND available_at <= {now}) OR (status = 'Leased' AND lease_expires_at <= {now}))
              AND attempt_count < {maximumAttempts}
            ORDER BY available_at, created_at
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (item is null) { await transaction.CommitAsync(cancellationToken); return null; }
        var batch = await database.Batches.SingleAsync(x => x.BatchId == item.BatchId, cancellationToken);
        item.Lease(owner, now + leaseDuration, now); batch.MarkProcessingStarted(now);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(item.WorkItemId, batch.BatchId, owner, batch.MerchantId, batch.OriginalFilename,
            batch.OriginalObjectKey, batch.OriginalSha256, batch.ReceivedAt, batch.ProcessingStartedAt!.Value, item.AttemptCount);
    }

    public async Task CompleteAsync(ClaimedWork work, ProcessingResult result, ArtifactKeys keys, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var item = await database.BatchWorkItems.SingleAsync(x => x.WorkItemId == work.WorkItemId, cancellationToken);
        if (item.Status == WorkItemStatus.Completed) { await transaction.CommitAsync(cancellationToken); return; }
        if (item.Status != WorkItemStatus.Leased || item.LeaseOwner != work.LeaseOwner) throw new InvalidOperationException("Work lease is no longer owned.");
        var batch = await database.Batches.SingleAsync(x => x.BatchId == work.BatchId, cancellationToken);
        var now = PostgreSqlTimestamp(timeProvider.GetUtcNow());
        batch.CompleteProcessing(result.TotalRows, result.Accepted.Count, result.RowRejectedCount, keys.Accepted, keys.Rejected, keys.Summary, result.Status, now);
        item.Complete(now); await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailAsync(ClaimedWork work, string safeError, int maximumAttempts, CancellationToken cancellationToken)
    {
        var item = await database.BatchWorkItems.SingleAsync(x => x.WorkItemId == work.WorkItemId, cancellationToken);
        if (item.Status != WorkItemStatus.Leased || item.LeaseOwner != work.LeaseOwner) return;
        var now = PostgreSqlTimestamp(timeProvider.GetUtcNow()); item.ReleaseOrFail(safeError, maximumAttempts, now);
        if (item.Status == WorkItemStatus.Failed)
            (await database.Batches.SingleAsync(x => x.BatchId == work.BatchId, cancellationToken)).MarkProcessingFailed(now);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset PostgreSqlTimestamp(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % 10), value.Offset);
}
