using System.Security.Cryptography;

namespace BatchDemo.Infrastructure.ObjectStorage;

internal sealed class HashingReadStream(Stream inner) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _completed;

    public long BytesRead { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => inner.CanSeek ? inner.Length - inner.Position + BytesRead : throw new NotSupportedException();
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

    public string CompleteHash()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The hash has already been completed.");
        }

        _completed = true;
        return Convert.ToHexStringLower(_hash.GetHashAndReset());
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        Append(buffer.AsSpan(offset, read));
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        Append(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        Append(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadArrayAsync(buffer, offset, count, cancellationToken);
    }

    private async Task<int> ReadArrayAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        Append(buffer.AsSpan(offset, read));
        return read;
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 0)
        {
            _hash.AppendData(bytes);
            BytesRead += bytes.Length;
        }
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
