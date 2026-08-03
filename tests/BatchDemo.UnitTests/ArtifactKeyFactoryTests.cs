using BatchDemo.Application;

namespace BatchDemo.UnitTests;

public sealed class ArtifactKeyFactoryTests
{
    [Theory]
    [InlineData("../../secrets.csv", "secrets.csv")]
    [InlineData("..\\..\\secrets.csv", "secrets.csv")]
    [InlineData("folder/merchant batch.csv", "merchant_batch.csv")]
    public void Original_does_not_trust_raw_filename(string rawFilename, string expectedFilename)
    {
        var batchId = Guid.Parse("65d35db0-78f7-437d-82f1-8e2b70df65e2");
        var sanitized = ArtifactKeyFactory.SanitizeFilename(rawFilename);

        var key = ArtifactKeyFactory.Original("merchant/demo", batchId, sanitized);

        Assert.DoesNotContain("..", key);
        Assert.DoesNotContain("\\", key);
        Assert.StartsWith("merchants/merchant_demo/batches/65d35db0-78f7-437d-82f1-8e2b70df65e2/original/", key);
        Assert.Equal(expectedFilename, key.Split('/').Last());
    }
}
