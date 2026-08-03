using BatchDemo.Domain;

namespace BatchDemo.Application;

public static class DuplicateClassifier
{
    public static void Apply(Batch candidate, Guid? canonicalBatchId, DateTimeOffset now)
    {
        if (canonicalBatchId.HasValue)
        {
            candidate.MarkDuplicate(canonicalBatchId.Value, now);
        }
    }
}
