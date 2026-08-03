namespace BatchDemo.Application.Abstractions;

public interface IOriginalObjectStore
{
    Task<StoredOriginal> StoreAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string objectKey, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task PublishUtf8IfAbsentAsync(string objectKey, string content, string contentType, CancellationToken cancellationToken);
}

public sealed record StoredOriginal(string ObjectKey, string Sha256, long Size);
