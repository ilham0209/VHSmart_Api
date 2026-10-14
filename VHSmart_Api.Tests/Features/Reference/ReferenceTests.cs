using VHSmart_Api.Features.Reference;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Reference;

// Reference menu (spec 17.1): the read-only card list (own company, Name asc, soft-deleted
// and foreign rows out) and the icon stream with its 404s - the admin twin's behaviour
// behind this screen's own permission.
public class ReferenceTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartReferenceTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public ReferenceTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_ReturnsOwnRowsSortedByNameWithoutForeignOrDeletedRows()
    {
        var user = ReferenceTestData.UserA();
        var db = await ReferenceTestData.CreateDbAsync(user);
        await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyA, "Verify Halal",
            description: "Halal verification portal");
        await ReferenceTestData.SeedWebLinkAsync(db, ReferenceTestData.CompanyA, "VH Smart");
        await ReferenceTestData.SeedWebLinkAsync(db, ReferenceTestData.CompanyA, "Seri Rasa");
        await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyA, "Deleted Link", isDeleted: true);
        await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyB, "Foreign Link");

        var cards = await new GetReferenceWebLinksHandler(db, user)
            .Handle(new GetReferenceWebLinksQuery(), CancellationToken.None);

        Assert.Equal(["Seri Rasa", "Verify Halal", "VH Smart"], cards.Select(card => card.Name));
        var verifyHalal = Assert.Single(cards, card => card.Name == "Verify Halal");
        Assert.Equal("Halal verification portal", verifyHalal.Description);
        Assert.Equal("https://verify-halal.example.org", verifyHalal.Webpage);
        Assert.NotEqual(Guid.Empty, verifyHalal.Id);
        Assert.DoesNotContain(cards, card => card.Name == "Deleted Link");
        Assert.DoesNotContain(cards, card => card.Name == "Foreign Link");
    }

    [Fact]
    public async Task Handle_RowWithIcon_StreamsTheStoredBytes()
    {
        var db = await ReferenceTestData.CreateDbAsync(ReferenceTestData.UserA());
        var icon = await _storage.SaveAsync(
            new MemoryStream([137, 80, 78, 71]), "icon.png", "image/png");
        var id = await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyA, "Verify Halal", icon: icon);

        var response = await new GetReferenceWebLinkIconHandler(db, _storage)
            .Handle(new GetReferenceWebLinkIconQuery(id), CancellationToken.None);

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
        var db = await ReferenceTestData.CreateDbAsync(ReferenceTestData.UserA());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetReferenceWebLinkIconHandler(db, _storage)
                .Handle(new GetReferenceWebLinkIconQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Web link not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(
            databaseName,
            new TestCurrentUser(Guid.NewGuid().ToString(), ReferenceTestData.CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignId = await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyB, "Foreign Link");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetReferenceWebLinkIconHandler(db, _storage)
                .Handle(new GetReferenceWebLinkIconQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingBytesOnDisk_ThrowsNotFound()
    {
        var db = await ReferenceTestData.CreateDbAsync(ReferenceTestData.UserA());
        var icon = await _storage.SaveAsync(
            new MemoryStream([137, 80, 78, 71]), "icon.png", "image/png");
        var id = await ReferenceTestData.SeedWebLinkAsync(
            db, ReferenceTestData.CompanyA, "Verify Halal", icon: icon);
        File.Delete(Path.Combine(_storageRoot, icon.StorageKey));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetReferenceWebLinkIconHandler(db, _storage)
                .Handle(new GetReferenceWebLinkIconQuery(id), CancellationToken.None));
    }
}
