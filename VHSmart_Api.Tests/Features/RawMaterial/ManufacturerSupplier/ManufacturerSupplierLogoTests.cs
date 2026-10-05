using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class ManufacturerSupplierLogoTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartManufacturerSupplierLogoTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public ManufacturerSupplierLogoTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static readonly Guid CompanyA = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<ManufacturerSupplierEntity> SeedRowAsync(TestableVHSmartDbContext db)
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyA,
            Type = ManufacturerSupplierType.ManufacturerOnly,
            ManufacturerName = "Santan Foods",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = "manufacturer@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static FormFile Logo(string fileName = "logo.png") =>
        new(new MemoryStream([1, 2, 3]), 0, 3, "File", fileName);

    [Fact]
    public async Task Handle_UploadThenGet_StreamsStoredBytes()
    {
        var db = await CreateDbAsync(UserA());
        var row = await SeedRowAsync(db);

        await new UploadManufacturerSupplierLogoHandler(db, _storage, NullLogger())
            .Handle(new UploadManufacturerSupplierLogoCommand(row.Id, Logo()), CancellationToken.None);

        var response = await new GetManufacturerSupplierLogoHandler(db, _storage)
            .Handle(new GetManufacturerSupplierLogoQuery(row.Id), CancellationToken.None);

        Assert.Equal("image/png", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("\u0001\u0002\u0003", await reader.ReadToEndAsync());

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal("logo.png", stored.Logo?.FileName);
    }

    [Fact]
    public async Task Handle_UploadReplacesThePreviousLogo()
    {
        var db = await CreateDbAsync(UserA());
        var row = await SeedRowAsync(db);
        var upload = new UploadManufacturerSupplierLogoHandler(db, _storage, NullLogger());

        await upload.Handle(
            new UploadManufacturerSupplierLogoCommand(row.Id, Logo("first.png")), CancellationToken.None);
        var first = (await db.ManufacturerSuppliers.AsNoTracking().SingleAsync()).Logo;
        await upload.Handle(
            new UploadManufacturerSupplierLogoCommand(row.Id, Logo("second.png")), CancellationToken.None);

        var updated = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal("second.png", updated.Logo?.FileName);
        Assert.NotEqual(first?.StorageKey, updated.Logo?.StorageKey);
    }

    [Fact]
    public async Task Handle_UploadDisallowedType_ThrowsBusinessRule()
    {
        var db = await CreateDbAsync(UserA());
        var row = await SeedRowAsync(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new UploadManufacturerSupplierLogoHandler(db, _storage, NullLogger())
                .Handle(
                    new UploadManufacturerSupplierLogoCommand(row.Id, Logo("logo.exe")),
                    CancellationToken.None));

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Null(stored.Logo);
    }

    [Fact]
    public async Task Handle_UploadUnknownRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UploadManufacturerSupplierLogoHandler(db, _storage, NullLogger())
                .Handle(
                    new UploadManufacturerSupplierLogoCommand(Guid.NewGuid(), Logo()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GetWithoutLogo_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var row = await SeedRowAsync(db);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetManufacturerSupplierLogoHandler(db, _storage)
                .Handle(new GetManufacturerSupplierLogoQuery(row.Id), CancellationToken.None));

        Assert.Equal("No logo.", exception.Message);
    }

    [Fact]
    public async Task Handle_GetUnknownRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetManufacturerSupplierLogoHandler(db, _storage)
                .Handle(new GetManufacturerSupplierLogoQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GetAnotherCompanysRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignRow = new ManufacturerSupplierEntity
        {
            CompanyId = Guid.NewGuid(),
            Type = ManufacturerSupplierType.ManufacturerOnly,
            ManufacturerName = "Other maker",
            ManufacturerAddress = "Jalan Gombak 9",
            ManufacturerEmail = "other@example.com"
        };
        db.ManufacturerSuppliers.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetManufacturerSupplierLogoHandler(db, _storage)
                .Handle(new GetManufacturerSupplierLogoQuery(foreignRow.Id), CancellationToken.None));
    }

    private static Microsoft.Extensions.Logging.ILogger<UploadManufacturerSupplierLogoHandler> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<UploadManufacturerSupplierLogoHandler>.Instance;
}
