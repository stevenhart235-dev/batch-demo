using Amazon.S3;
using Amazon.S3.Model;
using BatchDemo.Application.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace BatchDemo.Infrastructure.ObjectStorage;

public sealed class S3OriginalObjectStore(IAmazonS3 client, IOptions<S3Options> options) : IOriginalObjectStore
{
    private readonly S3Options _options = options.Value;

    public async Task<StoredOriginal> StoreAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var hashingStream = new HashingReadStream(content);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = hashingStream,
            ContentType = contentType,
            AutoCloseStream = false,
            IfNoneMatch = "*"
        };

        await client.PutObjectAsync(request, cancellationToken);
        return new StoredOriginal(objectKey, hashingStream.CompleteHash(), hashingStream.BytesRead);
    }

    public async Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(_options.BucketName, objectKey, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await client.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);
        return new ResponseStream(response);
    }

    public async Task PublishUtf8IfAbsentAsync(string objectKey, string content, string contentType, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = new MemoryStream(bytes),
                ContentType = contentType,
                IfNoneMatch = "*"
            }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            using var existing = await client.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);
            using var memory = new MemoryStream();
            await existing.ResponseStream.CopyToAsync(memory, cancellationToken);
            if (!memory.ToArray().AsSpan().SequenceEqual(bytes))
                throw new InvalidOperationException($"Existing artifact '{objectKey}' does not match deterministic output.");
        }
    }

    private sealed class ResponseStream(GetObjectResponse response) : Stream
    {
        private Stream Inner => response.ResponseStream;
        public override bool CanRead => Inner.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => response.ContentLength; public override long Position { get => Inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => Inner.Flush(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) response.Dispose(); base.Dispose(disposing); }
    }
}
