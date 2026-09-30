using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class UpdateWebLinkTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartWebLinkUpdateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public UpdateWebLinkTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IFormFile IconFile(string fileName = "second.png")
    {
        var bytes = (byte[])[137, 80, 78, 71];
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "image/png"
        };
        return file;
    }

    private static WebLinkEntity NewRow(Guid companyId, string name, string iconFileName = "icon.png") =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Webpage = "https://verifyhalal.com",
            Description = "Halal verification",
            Icon = new()
            {
                FileName = iconFileName,
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "image/png"
            }
        };

    private UpdateWebLinkHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UpdateWebLinkHandler>.Instance);

    [Fact]
    public async Task Handle_WithoutFile_UpdatesFieldsAndKeepsTheIcon()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        var previousKey = row.Icon.StorageKey;

        var response = await NewHandler(db, user).Handle(
            new UpdateWebLinkCommand(row.Id, "Verify Halal MY", "https://verify.my", null, null),
            CancellationToken.None);

        Assert.Equal("Verify Halal MY", response.Name);
        Assert.Equal("icon.png", response.IconFileName);

        var stored = await db.WebLinks.AsNoTracking().SingleAsync();
        Assert.Equal(previousKey, stored.Icon.StorageKey);
        Assert.Equal(CompanyA, stored.CompanyId);
    }

    [Fact]
    public async Task Handle_WithFile_ReplacesIconAndDeletesThePreviousBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        var previousKey = row.Icon.StorageKey;
        // The seeded icon has no bytes on disk; write some so the deletion is observable.
        Directory.CreateDirectory(_storageRoot);
        await File.WriteAllTextAsync(Path.Combine(_storageRoot, previousKey), "old");

        var response = await NewHandler(db, user).Handle(
            new UpdateWebLinkCommand(row.Id, "Verify Halal", "https://verifyhalal.com", "Updated", IconFile()),
            CancellationToken.None);

        Assert.Equal("second.png", response.IconFileName);

        var stored = await db.WebLinks.AsNoTracking().SingleAsync();
        Assert.Equal("second.png", stored.Icon.FileName);
        Assert.NotEqual(previousKey, stored.Icon.StorageKey);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Icon.StorageKey)));
        Assert.False(File.Exists(Path.Combine(_storageRoot, previousKey)));
    }

    [Fact]
    public async Task Handle_DuplicateNameExcludingSelf_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var self = NewRow(CompanyA, "Verify Halal");
        var other = NewRow(CompanyA, "Santan");
        db.WebLinks.AddRange(self, other);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(
                new UpdateWebLinkCommand(self.Id, "Santan", "https://santan.example", null, null),
                CancellationToken.None));

        // Keeping its own name is not a duplicate.
        var unchanged = await NewHandler(db, user).Handle(
            new UpdateWebLinkCommand(self.Id, "Verify Halal", "https://verifyhalal.com", null, null),
            CancellationToken.None);
        Assert.Equal("Verify Halal", unchanged.Name);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRuleAndKeepsTheOldIcon()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();
        var previousKey = row.Icon.StorageKey;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(
                new UpdateWebLinkCommand(row.Id, "Renamed", "https://verifyhalal.com", null, IconFile("doc.pdf")),
                CancellationToken.None));

        var stored = await db.WebLinks.AsNoTracking().SingleAsync();
        Assert.Equal("Verify Halal", stored.Name);
        Assert.Equal("icon.png", stored.Icon.FileName);
        Assert.Equal(previousKey, stored.Icon.StorageKey);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewHandler(db, user).Handle(
                new UpdateWebLinkCommand(Guid.NewGuid(), "Name", "https://example.org", null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B link");
        db.WebLinks.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewHandler(db, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA)).Handle(
                new UpdateWebLinkCommand(foreignRow.Id, "Renamed", "https://example.org", null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var validator = new UpdateWebLinkValidator();

        var result = await validator.ValidateAsync(new UpdateWebLinkCommand(
            Guid.Empty, string.Empty, string.Empty, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Webpage");
    }

    [Fact]
    public async Task Validator_FullValidCommandWithoutFile_Passes()
    {
        var validator = new UpdateWebLinkValidator();

        var result = await validator.ValidateAsync(new UpdateWebLinkCommand(
            Guid.NewGuid(), "Verify Halal", "https://verifyhalal.com", null, null));

        Assert.True(result.IsValid);
    }
}
