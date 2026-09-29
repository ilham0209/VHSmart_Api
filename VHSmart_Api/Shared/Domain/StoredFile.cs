namespace VHSmart_Api.Shared.Domain;

// The File column group (Database.md section 1). The row keeps only these four values; the
// bytes live in file storage under StorageKey.
public class StoredFile
{
    public string FileName { get; set; } = string.Empty;    // nvarchar(260), original name shown to the user
    public string StorageKey { get; set; } = string.Empty;  // nvarchar(100), generated GUID
    public string ContentType { get; set; } = string.Empty; // nvarchar(100)
    public long SizeBytes { get; set; }
}
