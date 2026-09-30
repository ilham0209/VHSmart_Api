using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class GetCertificationBodyLogoTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartCbLogoGetTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public GetCertificationBodyLogoTests()
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

    [Fact]
    public async Task Handle_WithLogo_StreamsStoredBytes()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db);
        var upload = new UploadCertificationBodyLogoHandler(
            db, user, _storage, NullLogger());
        await upload.Handle(
            new UploadCertificationBodyLogoCommand(
                row.Id,
                new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "File", "logo.png")),
            CancellationToken.None);

        var response = await new GetCertificationBodyLogoHandler(db, user, _storage)
            .Handle(new GetCertificationBodyLogoQuery(row.Id), CancellationToken.None);

        Assert.Equal("image/png", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("\u0001\u0002\u0003", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Handle_WithoutLogo_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificationBodyLogoHandler(db, user, _storage)
                .Handle(new GetCertificationBodyLogoQuery(row.Id), CancellationToken.None));

        Assert.Equal("No logo.", exception.Message);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificationBodyLogoHandler(db, user, _storage)
                .Handle(new GetCertificationBodyLogoQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetCertificationBodyLogoHandler(db, companyUser, _storage)
                .Handle(new GetCertificationBodyLogoQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private static Microsoft.Extensions.Logging.ILogger<UploadCertificationBodyLogoHandler> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<UploadCertificationBodyLogoHandler>.Instance;
}
