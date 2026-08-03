using BatchDemo.Application;

namespace BatchDemo.UnitTests;

public sealed class MoneyNormalizerTests
{
    [Theory]
    [InlineData("24.95", 2495)]
    [InlineData("24", 2400)]
    [InlineData("0.01", 1)]
    public void Converts_exact_minor_units(string input, long expected)
    { Assert.True(MoneyNormalizer.TryToMinorUnits(input, out var actual)); Assert.Equal(expected, actual); }

    [Theory]
    [InlineData("1.001")]
    [InlineData("0")]
    [InlineData("-1.00")]
    [InlineData("1e2")]
    public void Rejects_invalid_amounts(string input) => Assert.False(MoneyNormalizer.TryToMinorUnits(input, out _));
}
