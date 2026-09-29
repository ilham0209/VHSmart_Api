using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Shared.Infrastructure.Storage;

// Server-side upload rules (spec 21.10, D-22). The legacy system checked size and type only in
// the browser; the rebuild rejects the file here, before anything is stored. The screens pass
// their own list and limit when they differ from the defaults (premise: PDF only).
public static class FileValidation
{
    public const long DefaultMaxSizeBytes = 10 * 1024 * 1024; // D-22
    public const int FileNameMaxLength = 260;                  // File column group (Database.md section 1)

    public static readonly IReadOnlyList<string> DefaultAllowedExtensions = ["pdf", "docx", "xlsx", "jpg", "jpeg", "png"];
    public static readonly IReadOnlyList<string> PdfOnlyExtensions = ["pdf"];
    public static readonly IReadOnlyList<string> ImageExtensions = ["jpg", "jpeg", "png"];

    public static void Validate(string? fileName, long sizeBytes, IReadOnlyList<string>? allowedExtensions = null, long? maxBytes = null)
    {
        var allowed = allowedExtensions ?? DefaultAllowedExtensions;
        var limit = maxBytes ?? DefaultMaxSizeBytes;

        if (sizeBytes > limit)
            throw new BusinessRuleException($"The file size exceeds the limit of {limit / 1024d / 1024d:0.##} MB.");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new BusinessRuleException($"File type is not allowed. Allowed types: {string.Join(", ", allowed)}.");

        if (fileName.Length > FileNameMaxLength)
            throw new BusinessRuleException($"File name is too long (maximum {FileNameMaxLength} characters).");

        var extension = Path.GetExtension(fileName).TrimStart('.'); // "report.PDF" -> "PDF"
        if (extension.Length == 0 || !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException($"File type is not allowed. Allowed types: {string.Join(", ", allowed)}.");
    }
}
