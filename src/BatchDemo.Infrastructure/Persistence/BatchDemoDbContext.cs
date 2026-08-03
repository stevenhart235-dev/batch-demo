using BatchDemo.Domain;
using Microsoft.EntityFrameworkCore;

namespace BatchDemo.Infrastructure.Persistence;

public sealed class BatchDemoDbContext(DbContextOptions<BatchDemoDbContext> options) : DbContext(options)
{
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchWorkItem> BatchWorkItems => Set<BatchWorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var batch = modelBuilder.Entity<Batch>();
        batch.ToTable("batches", table =>
        {
            table.HasCheckConstraint("ck_batches_sha256", "length(original_sha256) = 64");
            table.HasCheckConstraint(
                "ck_batches_duplicate_canonical",
                "(status = 'Duplicate' AND canonical_batch_id IS NOT NULL) OR (status <> 'Duplicate' AND canonical_batch_id IS NULL)");
        });
        batch.HasKey(x => x.BatchId).HasName("pk_batches");
        batch.Property(x => x.BatchId).HasColumnName("batch_id");
        batch.Property(x => x.MerchantId).HasColumnName("merchant_id").HasMaxLength(200).IsRequired();
        batch.Property(x => x.OriginalFilename).HasColumnName("original_filename").HasMaxLength(200).IsRequired();
        batch.Property(x => x.OriginalObjectKey).HasColumnName("original_object_key").HasMaxLength(1024).IsRequired();
        batch.Property(x => x.OriginalSha256).HasColumnName("original_sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        batch.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        batch.Property(x => x.CanonicalBatchId).HasColumnName("canonical_batch_id");
        batch.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        batch.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        batch.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        batch.Property(x => x.AcceptedCount).HasColumnName("accepted_count");
        batch.Property(x => x.RejectedCount).HasColumnName("rejected_count");
        batch.Property(x => x.TotalRowCount).HasColumnName("total_row_count");
        batch.Property(x => x.AcceptedArtifactKey).HasColumnName("accepted_artifact_key").HasMaxLength(1024);
        batch.Property(x => x.RejectedArtifactKey).HasColumnName("rejected_artifact_key").HasMaxLength(1024);
        batch.Property(x => x.SummaryArtifactKey).HasColumnName("summary_artifact_key").HasMaxLength(1024);
        batch.Property(x => x.ProcessingStartedAt).HasColumnName("processing_started_at");
        batch.Property(x => x.ProcessingCompletedAt).HasColumnName("processing_completed_at");
        batch.HasIndex(x => x.OriginalObjectKey).IsUnique().HasDatabaseName("ux_batches_original_object_key");
        batch.HasIndex(x => new { x.MerchantId, x.OriginalSha256 })
            .IsUnique()
            .HasDatabaseName("ux_batches_canonical_delivery")
            .HasFilter("canonical_batch_id IS NULL");
        batch.HasOne(x => x.CanonicalBatch)
            .WithMany()
            .HasForeignKey(x => x.CanonicalBatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_batches_canonical_batch");
        var workItem = modelBuilder.Entity<BatchWorkItem>();
        workItem.ToTable("batch_work_items", table =>
            table.HasCheckConstraint("ck_batch_work_items_attempt_count", "attempt_count >= 0"));
        workItem.HasKey(x => x.WorkItemId).HasName("pk_batch_work_items");
        workItem.Property(x => x.WorkItemId).HasColumnName("work_item_id");
        workItem.Property(x => x.BatchId).HasColumnName("batch_id");
        workItem.Property(x => x.WorkType).HasColumnName("work_type").HasMaxLength(80).IsRequired();
        workItem.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        workItem.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        workItem.Property(x => x.AvailableAt).HasColumnName("available_at").IsRequired();
        workItem.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);
        workItem.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        workItem.Property(x => x.LastError).HasColumnName("last_error");
        workItem.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        workItem.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        workItem.HasIndex(x => x.BatchId).IsUnique().HasDatabaseName("ux_batch_work_items_batch_id");
        workItem.HasIndex(x => new { x.Status, x.AvailableAt }).HasDatabaseName("ix_batch_work_items_available");
        workItem.HasOne(x => x.Batch)
            .WithMany(x => x.WorkItems)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_batch_work_items_batch");
    }
}
