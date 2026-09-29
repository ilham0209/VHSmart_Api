using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
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
    public async Task SaveAsync_WritesFileAndReturnsGuidKeyWithOriginalName()
    {
        using var content = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var stored = await _storage.SaveAsync(content, "report.PDF", "application/pdf");

        Assert.True(Guid.TryParse(stored.StorageKey, out _));
        Assert.Equal("report.PDF", stored.FileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(5, stored.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_rootPath, stored.StorageKey)));
        Assert.Empty(Directory.GetFiles(_rootPath, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_WithoutContentType_FallsBackToOctetStream()
    {
        using var content = new MemoryStream(new byte[] { 1 });

        var stored = await _storage.SaveAsync(content, "a.pdf", contentType: string.Empty);

        Assert.Equal("application/octet-stream", stored.ContentType);
    }

    [Fact]
    public async Task OpenReadAsync_SavedFile_ReturnsSameBytes()
    {
        var payload = "halal certificate"u8.ToArray();
        var stored = await _storage.SaveAsync(new MemoryStream(payload), "certificate.pdf", "application/pdf");

        await using var content = await _storage.OpenReadAsync(stored.StorageKey);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);

        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_UnknownKey_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await _storage.OpenReadAsync(Guid.NewGuid().ToString()));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..")]
    [InlineData("sub/file.txt")]
    [InlineData("sub\\file.txt")]
    [InlineData("")]
    public async Task OpenReadAsync_KeyOutsideStorageRoot_ThrowsNotFound(string storageKey)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await _storage.OpenReadAsync(storageKey));
    }

    [Fact]
    public async Task DeleteAsync_SavedFile_IsRemovedAndDeletingAgainIsNotAnError()
    {
        var stored = await _storage.SaveAsync(new MemoryStream(new byte[] { 1 }), "a.pdf", "application/pdf");

        await _storage.DeleteAsync(stored.StorageKey);
        Assert.False(File.Exists(Path.Combine(_rootPath, stored.StorageKey)));

        await _storage.DeleteAsync(stored.StorageKey);
    }

    [Fact]
    public async Task DeleteAsync_KeyOutsideStorageRoot_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await _storage.DeleteAsync("../outside.txt"));
    }
}
