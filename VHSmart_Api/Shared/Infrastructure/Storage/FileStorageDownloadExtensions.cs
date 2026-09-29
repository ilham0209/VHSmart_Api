using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Shared.Domain;

namespace VHSmart_Api.Shared.Infrastructure.Storage;

// Download helper: opens the stored bytes and builds the response. The endpoint keeps the same
// permission as viewing the record (CodingRules 10); a missing or crafted key surfaces as
// NotFoundException, which the middleware turns into 404.
public static class FileStorageDownloadExtensions
{
    private const string FallbackContentType = "application/octet-stream";

    public static async Task<FileStreamResult> DownloadAsync(
        this IFileStorage storage,
        StoredFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? FallbackContentType : file.ContentType;

        return new FileStreamResult(content, contentType) { FileDownloadName = file.FileName };
    }
}
