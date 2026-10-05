using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class UpdateManufacturerSupplierTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

    private static UpdateManufacturerSupplierCommand Command(
        Guid id,
        Guid countryId,
        ManufacturerSupplierType? type = ManufacturerSupplierType.Both,
        string? manufacturerEmail = "manufacturer@example.com",
        string? supplierEmail = "supplier@example.com") =>
        new(
            id,
            type,
            "Santan Foods Sdn Bhd",
            "202301001234",
            null,
            "Jalan Gombak 1",
            countryId,
            "Aminah",
            "0380000000",
            manufacturerEmail,
            "https://santan.example.com",
            "Santan Supplies",
            "Jalan Gombak 2",
            countryId,
            "Fatimah",
            "0380001111",
            supplierEmail);

    private static async Task<ManufacturerSupplierEntity> SeedRowAsync(
        TestableVHSmartDbContext db,
        string name = "Santan Foods",
        string manufacturerEmail = "manufacturer@example.com",
        string supplierEmail = "supplier@example.com")
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyA,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = name,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerCountryId = await MalaysiaIdAsync(db),
            ManufacturerEmail = manufacturerEmail,
            SupplierName = name,
            SupplierAddress = "Jalan Gombak 2",
            SupplierCountryId = await MalaysiaIdAsync(db),
            SupplierEmail = supplierEmail
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_ExistingRow_StoresChanges()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        var row = await SeedRowAsync(db);

        var response = await new UpdateManufacturerSupplierHandler(db, user)
            .Handle(Command(row.Id, malaysiaId, manufacturerEmail: "updated@example.com"), CancellationToken.None);

        Assert.Equal("updated@example.com", response.ManufacturerEmail);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal("updated@example.com", stored.ManufacturerEmail);
        Assert.Equal(CompanyA, stored.CompanyId);
    }

    [Fact]
    public async Task Handle_TypeChangedToManufacturerOnly_ClearsSupplierHalfAndFreesItsEmail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        var row = await SeedRowAsync(db);

        var response = await new UpdateManufacturerSupplierHandler(db, user)
            .Handle(
                Command(row.Id, malaysiaId, ManufacturerSupplierType.ManufacturerOnly),
                CancellationToken.None);

        Assert.Null(response.SupplierName);
        Assert.Null(response.SupplierEmail);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Null(stored.SupplierAddress);
        Assert.Null(stored.SupplierEmail);

        // The freed supplier e-mail can be used by a new row (spec 21.9, soft/cleared values
        // do not hold the unique slot).
        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(
                new CreateManufacturerSupplierCommand(
                    ManufacturerSupplierType.SupplierOnly,
                    null, null, null, null, null, null, null, null, null,
                    "Other Supplies", "Jalan Gombak 3", malaysiaId, "Zainab", "0380002222",
                    "supplier@example.com"),
                CancellationToken.None);

        Assert.Equal(2, await db.ManufacturerSuppliers.CountAsync());
    }

    [Fact]
    public async Task Handle_DuplicateManufacturerEmailOfAnotherRow_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        await SeedRowAsync(db, manufacturerEmail: "first@example.com");
        var second = await SeedRowAsync(db, name: "Second Foods", manufacturerEmail: "second@example.com");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateManufacturerSupplierHandler(db, user)
                .Handle(
                    Command(second.Id, malaysiaId, manufacturerEmail: "first@example.com"),
                    CancellationToken.None));

        Assert.Equal("A manufacturer with this e-mail already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_KeepingItsOwnEmail_DoesNotConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        var row = await SeedRowAsync(db);

        var response = await new UpdateManufacturerSupplierHandler(db, user)
            .Handle(Command(row.Id, malaysiaId), CancellationToken.None);

        Assert.Equal("manufacturer@example.com", response.ManufacturerEmail);
        Assert.Equal("supplier@example.com", response.SupplierEmail);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateManufacturerSupplierHandler(db, user)
                .Handle(Command(Guid.NewGuid(), malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var userA = UserA();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var malaysiaId = await MalaysiaIdAsync(db);
        var foreignRow = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyB,
            Type = ManufacturerSupplierType.ManufacturerOnly,
            ManufacturerName = "Company B maker",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerCountryId = malaysiaId,
            ManufacturerEmail = "b@example.com"
        };
        db.ManufacturerSuppliers.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateManufacturerSupplierHandler(db, userA)
                .Handle(Command(foreignRow.Id, malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingType_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var malaysiaId = await MalaysiaIdAsync(db);
        var validator = new UpdateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), malaysiaId) with { Type = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Type");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), await MalaysiaIdAsync(db)));

        Assert.True(result.IsValid);
    }
}
