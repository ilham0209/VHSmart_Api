using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class UploadCertificationBodyLogoTests : IDisposable
{
    private const string PngName = "logo.png";

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartCbLogoTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public UploadCertificationBodyLogoTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<CertificationBodyEntity> SeedRowAsync(TestableVHSmartDbContext db)
    {
        var malaysiaId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var row = new CertificationBodyEntity { Name = "JAKIM", CountryId = malaysiaId };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private UploadCertificationBodyLogoCommand FileCommand(Guid id, string fileName, byte[]? content = null)
    {
        var bytes = content ?? [137, 80, 78, 71]; // PNG magic bytes, enough for storage
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, nameof(UploadCertificationBodyLogoCommand.File), fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "image/png"
        };
        return new UploadCertificationBodyLogoCommand(id, file);
    }

    private UploadCertificationBodyLogoHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadCertificationBodyLogoHandler>.Instance);

    [Fact]
    public async Task Handle_ValidImage_StoresFileAndSetsLogo()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db);
        var handler = NewHandler(db, user);

        var response = await handler.Handle(FileCommand(row.Id, PngName), CancellationToken.None);

        Assert.Equal(PngName, response.FileName);
        Assert.Equal("image/png", response.ContentType);

        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.NotNull(stored.Logo);
        Assert.Equal("image/png", stored.Logo.ContentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Logo.StorageKey)));
    }

    [Fact]
    public async Task Handle_ReplacingLogo_DeletesPreviousFile()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db);
        var handler = NewHandler(db, user);
        await handler.Handle(FileCommand(row.Id, PngName), CancellationToken.None);
        var previousKey = (await db.CertificationBodies.AsNoTracking().SingleAsync()).Logo!.StorageKey;

        await handler.Handle(FileCommand(row.Id, "second.png"), CancellationToken.None);

        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.Equal("second.png", stored.Logo!.FileName);
        Assert.False(File.Exists(Path.Combine(_storageRoot, previousKey)));
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Logo.StorageKey)));
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRule()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db);
        var handler = NewHandler(db, user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(FileCommand(row.Id, "document.pdf"), CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.Null(stored.Logo);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var handler = NewHandler(db, user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(FileCommand(Guid.NewGuid(), PngName), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));
        var handler = NewHandler(db, new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(FileCommand(Guid.NewGuid(), PngName), CancellationToken.None));
    }

    [Fact]
    public void Validator_MissingFile_Fails()
    {
        var result = new UploadCertificationBodyLogoValidator().Validate(
            new UploadCertificationBodyLogoCommand(Guid.Empty, null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "File");
    }
}
