using System.Security.Cryptography;

namespace BatchDemo.Application;

public static class ExactByteSha256
{
    public static async Task<string> ComputeAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}
