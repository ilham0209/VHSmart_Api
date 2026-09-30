using VHSmart_Api.Shared.Domain;

namespace VHSmart_Api.Shared.Infrastructure.Storage;

// Bytes live in file storage, the database keeps only the File column group (CodingRules 10).
// Call FileValidation.Validate before SaveAsync; storage does not decide which types are allowed.
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
