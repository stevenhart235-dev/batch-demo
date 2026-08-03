using System.Text;

namespace BatchDemo.Application;

public static class ArtifactKeyFactory
{
    public static string SanitizeFilename(string? rawFilename)
    {
        var normalized = (rawFilename ?? string.Empty).Replace('\\', '/');
        var leaf = normalized[(normalized.LastIndexOf('/') + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(leaf))
        {
            return "batch.csv";
        }

        var output = new StringBuilder(leaf.Length);
        foreach (var character in leaf)
        {
            output.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_');
        }

        var sanitized = output.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "batch.csv" : sanitized[..Math.Min(200, sanitized.Length)];
    }

    public static string Original(string merchantId, Guid batchId, string sanitizedFilename)
    {
        return $"merchants/{SafeSegment(merchantId)}/batches/{batchId:D}/original/{SanitizeFilename(sanitizedFilename)}";
    }

    private static string SafeSegment(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            output.Append(char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_');
        }

        var sanitized = output.ToString();
        return string.IsNullOrWhiteSpace(sanitized) ? "merchant" : sanitized[..Math.Min(100, sanitized.Length)];
    }
}
