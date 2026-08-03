using System.Text;
using BatchDemo.Application;

namespace BatchDemo.UnitTests;

public sealed class ExactByteSha256Tests
{
    [Fact]
    public async Task Hash_is_deterministic_over_exact_bytes()
    {
        var lf = Encoding.UTF8.GetBytes("a,b\n1,2\n");
        var crlf = Encoding.UTF8.GetBytes("a,b\r\n1,2\r\n");

        var first = await ExactByteSha256.ComputeAsync(new MemoryStream(lf));
        var second = await ExactByteSha256.ComputeAsync(new MemoryStream(lf));
        var changedLineEndings = await ExactByteSha256.ComputeAsync(new MemoryStream(crlf));

        Assert.Equal(first, second);
        Assert.NotEqual(first, changedLineEndings);
        Assert.Equal("492d5ea496056f1a6a6592241032fab764c321596317930b4fa0e1e8bc3b7470", first);
    }
}
