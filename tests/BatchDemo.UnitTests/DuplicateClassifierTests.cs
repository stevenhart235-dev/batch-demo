using BatchDemo.Application;
using BatchDemo.Domain;

namespace BatchDemo.UnitTests;

public sealed class DuplicateClassifierTests
{
    [Fact]
    public void Existing_canonical_marks_candidate_duplicate()
    {
        var candidate = CreateBatch();
        var canonicalId = Guid.NewGuid();

        DuplicateClassifier.Apply(candidate, canonicalId, DateTimeOffset.UtcNow);

        Assert.Equal(BatchStatus.Duplicate, candidate.Status);
        Assert.Equal(canonicalId, candidate.CanonicalBatchId);
    }

    [Fact]
    public void Missing_canonical_leaves_candidate_received()
    {
        var candidate = CreateBatch();

        DuplicateClassifier.Apply(candidate, null, DateTimeOffset.UtcNow);

        Assert.Equal(BatchStatus.Received, candidate.Status);
        Assert.Null(candidate.CanonicalBatchId);
    }

    private static Batch CreateBatch() => Batch.CreateReceived(
        Guid.NewGuid(),
        "merchant_demo",
        "batch.csv",
        "object-key",
        new string('a', 64),
        DateTimeOffset.UtcNow);
}
