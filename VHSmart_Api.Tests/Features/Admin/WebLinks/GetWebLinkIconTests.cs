using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class GetWebLinkIconTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartWebLinkIconTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public GetWebLinkIconTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<WebLinkEntity> SeedRowAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var icon = await _storage.SaveAsync(
            new MemoryStream([137, 80, 78, 71]), "icon.png", "image/png");
        var row = new WebLinkEntity
        {
            CompanyId = companyId,
            Name = "Verify Halal",
            Webpage = "https://verifyhalal.com",
            Icon = icon
        };
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_RowWithIcon_StreamsTheStoredBytes()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = await SeedRowAsync(db, CompanyA);

        var response = await new GetWebLinkIconHandler(db, _storage)
            .Handle(new GetWebLinkIconQuery(row.Id), CancellationToken.None);

        Assert.Equal("image/png", response.ContentType);
        byte[] bytes;
        using (response.Content) // the FileStream must be closed before Dispose wipes the root
        {
            using var memory = new MemoryStream();
            await response.Content.CopyToAsync(memory);
            bytes = memory.ToArray();
        }

        Assert.Equal((byte[])[137, 80, 78, 71], bytes);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWebLinkIconHandler(db, _storage)
                .Handle(new GetWebLinkIconQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = await SeedRowAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWebLinkIconHandler(db, _storage)
                .Handle(new GetWebLinkIconQuery(foreignRow.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingBytesOnDisk_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = await SeedRowAsync(db, CompanyA);
        File.Delete(Path.Combine(_storageRoot, row.Icon.StorageKey));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWebLinkIconHandler(db, _storage)
                .Handle(new GetWebLinkIconQuery(row.Id), CancellationToken.None));
    }
}
