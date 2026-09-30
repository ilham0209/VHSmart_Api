using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Storage;

public sealed class FileStorageDownloadExtensionsTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFileStorage _storage;

    public FileStorageDownloadExtensionsTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VHSmartTests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorage(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    [Fact]
    public async Task Download_StoredFile_ReturnsResponseWithOriginalNameAndBytes()
    {
        var stored = await _storage.SaveAsync(
            new MemoryStream("halal certificate"u8.ToArray()), "certificate.pdf", "application/pdf");

        var result = await _storage.DownloadAsync(stored);
        var content = await ReadContentAsync(result); // also releases the file handle

        Assert.Equal("certificate.pdf", result.FileDownloadName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("halal certificate", content);
    }

    [Fact]
    public async Task Download_UnknownKey_ThrowsNotFound()
    {
        var file = new StoredFile
        {
            FileName = "gone.pdf",
            StorageKey = Guid.NewGuid().ToString(),
            ContentType = "application/pdf"
        };

        await Assert.ThrowsAsync<NotFoundException>(() => _storage.DownloadAsync(file));
    }

    [Fact]
    public async Task Download_EmptyContentType_FallsBackToOctetStream()
    {
        var stored = await _storage.SaveAsync(new MemoryStream(new byte[] { 1 }), "a.pdf", "application/pdf");
        var file = new StoredFile
        {
            FileName = stored.FileName,
            StorageKey = stored.StorageKey,
            ContentType = " ",
            SizeBytes = stored.SizeBytes
        };

        var result = await _storage.DownloadAsync(file);
        await using (result.FileStream)
        {
            Assert.Equal("application/octet-stream", result.ContentType);
        }
    }

    // Reads the bytes the response would stream; the StreamReader also releases the file handle.
    private static async Task<string> ReadContentAsync(FileStreamResult result)
    {
        using var reader = new StreamReader(result.FileStream);
        return await reader.ReadToEndAsync();
    }
}
