using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Shared.Infrastructure.Storage;

// Local disk implementation first, blob later (CodingRules 10). The file is written under a
// generated GUID key (spec 21.10); the original name is kept only in the database.
public sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    private const string FallbackContentType = "application/octet-stream";

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootPath);

        // Write to a temporary file first: a failed upload never leaves a half-written file
        // that a later download would serve broken (spec 21.10 legacy defect).
        var storageKey = Guid.NewGuid().ToString("D");
        var temporaryPath = Path.Combine(rootPath, $"{storageKey}.tmp");
        var finalPath = Path.Combine(rootPath, storageKey);
        long sizeBytes;

        try
        {
            await using (var target = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(target, cancellationToken);
                sizeBytes = target.Length;
            }

            File.Move(temporaryPath, finalPath);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }

        return new StoredFile
        {
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? FallbackContentType : contentType,
            SizeBytes = sizeBytes
        };
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = KeyToPath(storageKey);

        try
        {
            return Task.FromResult<Stream>(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotFoundException("File not found.");
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = KeyToPath(storageKey);
        if (File.Exists(path))
            File.Delete(path); // deleting twice is not an error: the row is soft deleted anyway

        return Task.CompletedTask;
    }

    // StorageKey is a generated GUID. Anything with a separator or ".." must never reach
    // Path.Combine, or a crafted key could read outside the storage root.
    private string KeyToPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Contains('/', StringComparison.Ordinal)
            || storageKey.Contains('\\', StringComparison.Ordinal)
            || storageKey.Contains("..", StringComparison.Ordinal)
            || Path.GetFileName(storageKey) != storageKey)
            throw new NotFoundException("File not found.");

        return Path.Combine(rootPath, storageKey);
    }
}
