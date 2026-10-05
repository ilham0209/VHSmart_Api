using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class CreateManufacturerSupplierTests
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

    private static CreateManufacturerSupplierCommand Command(
        Guid countryId,
        ManufacturerSupplierType? type = ManufacturerSupplierType.Both,
        string? manufacturerEmail = "manufacturer@example.com",
        string? supplierEmail = "supplier@example.com",
        string manufacturerName = "Santan Foods Sdn Bhd") =>
        new(
            type,
            manufacturerName,
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

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithCompanyIdFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new CreateManufacturerSupplierHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        Assert.Equal(ManufacturerSupplierType.Both, response.Type);
        Assert.Equal("Santan Foods Sdn Bhd", response.ManufacturerName);
        Assert.Equal("Santan Supplies", response.SupplierName);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(malaysiaId, stored.ManufacturerCountryId);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_ManufacturerOnly_ClearsTheSupplierHalf()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(
                Command(malaysiaId, ManufacturerSupplierType.ManufacturerOnly),
                CancellationToken.None);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(ManufacturerSupplierType.ManufacturerOnly, stored.Type);
        Assert.NotNull(stored.ManufacturerName);
        Assert.Null(stored.SupplierName);
        Assert.Null(stored.SupplierAddress);
        Assert.Null(stored.SupplierCountryId);
        Assert.Null(stored.SupplierEmail);
    }

    [Fact]
    public async Task Handle_SupplierOnly_ClearsTheManufacturerHalf()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(
                Command(malaysiaId, ManufacturerSupplierType.SupplierOnly),
                CancellationToken.None);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(ManufacturerSupplierType.SupplierOnly, stored.Type);
        Assert.NotNull(stored.SupplierName);
        Assert.Null(stored.ManufacturerName);
        Assert.Null(stored.ManufacturerTypeId);
        Assert.Null(stored.ManufacturerEmail);
    }

    [Fact]
    public async Task Handle_DuplicateManufacturerEmail_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateManufacturerSupplierHandler(db, user)
                .Handle(
                    Command(malaysiaId, manufacturerName: "Second Manufacturer"),
                    CancellationToken.None));

        Assert.Equal("A manufacturer with this e-mail already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicateSupplierEmail_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateManufacturerSupplierHandler(db, user)
                .Handle(
                    Command(malaysiaId, manufacturerEmail: "other@example.com"),
                    CancellationToken.None));

        Assert.Equal("A supplier with this e-mail already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_SameEmailInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var malaysiaId = await MalaysiaIdAsync(db);

        var companyARow = await new CreateManufacturerSupplierHandler(db, userA)
            .Handle(Command(malaysiaId), CancellationToken.None);

        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        var companyBRow = await new CreateManufacturerSupplierHandler(dbB, userB)
            .Handle(Command(malaysiaId), CancellationToken.None);

        Assert.NotEqual(companyARow.Id, companyBRow.Id);
        Assert.Equal(CompanyB, (await dbB.ManufacturerSuppliers.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Handle_SupplierOnlyRow_DoesNotCollideOnTheAbsentManufacturerEmail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        // Both rows are supplier-only, so neither stores a manufacturer e-mail: the same
        // manufacturer e-mail on both must not collide.
        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(
                Command(malaysiaId, ManufacturerSupplierType.SupplierOnly),
                CancellationToken.None);
        await new CreateManufacturerSupplierHandler(db, user)
            .Handle(
                Command(
                    malaysiaId,
                    ManufacturerSupplierType.SupplierOnly,
                    supplierEmail: "other-supplier@example.com"),
                CancellationToken.None);

        Assert.Equal(2, await db.ManufacturerSuppliers.CountAsync());
    }

    [Fact]
    public async Task Validator_MissingType_FailsWithoutCheckingTheHalves()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(new CreateManufacturerSupplierCommand(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Type");
        // The halves are only judged once the type says which of them applies.
        Assert.DoesNotContain(result.Errors, failure => failure.PropertyName == "ManufacturerName");
        Assert.DoesNotContain(result.Errors, failure => failure.PropertyName == "SupplierName");
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(new CreateManufacturerSupplierCommand(
            ManufacturerSupplierType.Both, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerName");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerAddress");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerCountryId");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "SupplierName");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "SupplierAddress");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "SupplierCountryId");
    }

    [Fact]
    public async Task Validator_SupplierOnly_RequiresOnlyTheSupplierHalf()
    {
        var db = await CreateDbAsync(UserA());
        var malaysiaId = await MalaysiaIdAsync(db);
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(new CreateManufacturerSupplierCommand(
            ManufacturerSupplierType.SupplierOnly,
            null, null, null, null, null, null, null, null, null,
            "Santan Supplies", "Jalan Gombak 2", malaysiaId, "Fatimah", "0380001111",
            "supplier@example.com"));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, failure => failure.PropertyName == "ManufacturerName");
    }

    [Fact]
    public async Task Validator_ManufacturerOnly_MissingManufacturerFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var malaysiaId = await MalaysiaIdAsync(db);
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(new CreateManufacturerSupplierCommand(
            ManufacturerSupplierType.ManufacturerOnly,
            null, null, null, null, null, null, null, null, null,
            "Santan Supplies", "Jalan Gombak 2", malaysiaId, "Fatimah", "0380001111",
            "supplier@example.com"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerName");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerAddress");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerCountryId");
        Assert.DoesNotContain(result.Errors, failure => failure.PropertyName == "SupplierName");
    }

    [Fact]
    public async Task Validator_UnknownCountry_FailsWithCountryNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "ManufacturerCountryId" && failure.ErrorMessage == "Country not found.");
    }

    [Fact]
    public async Task Validator_UnknownManufacturerType_FailsWithManufacturerTypeNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var malaysiaId = await MalaysiaIdAsync(db);
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(
            Command(malaysiaId) with { ManufacturerTypeId = Guid.NewGuid() });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "ManufacturerTypeId"
                && failure.ErrorMessage == "Manufacturer type not found.");
    }

    [Fact]
    public async Task Validator_ManufacturerTypeOfAnotherCompany_Fails()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var userA = UserA();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var malaysiaId = await MalaysiaIdAsync(db);

        var foreignType = new GeneralDataEntity
        {
            CompanyId = CompanyB,
            Group = GeneralDataGroup.COMPANY,
            Category = "Manufacturer Type",
            Name = "Local Producer"
        };
        db.GeneralData.Add(foreignType);
        await db.SaveChangesAsync();

        var validator = new CreateManufacturerSupplierValidator(db, userA);
        var result = await validator.ValidateAsync(
            Command(malaysiaId) with { ManufacturerTypeId = foreignType.Id });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "ManufacturerTypeId"
                && failure.ErrorMessage == "Manufacturer type not found.");
    }

    [Fact]
    public async Task Validator_OwnManufacturerType_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        var ownType = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Manufacturer Type",
            Name = "Local Producer"
        };
        db.GeneralData.Add(ownType);
        await db.SaveChangesAsync();

        var validator = new CreateManufacturerSupplierValidator(db, user);
        var result = await validator.ValidateAsync(
            Command(malaysiaId) with { ManufacturerTypeId = ownType.Id });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var malaysiaId = await MalaysiaIdAsync(db);
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(
            Command(malaysiaId, manufacturerName: new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ManufacturerName");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateManufacturerSupplierValidator(db, UserA());

        var result = await validator.ValidateAsync(Command(await MalaysiaIdAsync(db)));

        Assert.True(result.IsValid);
    }
}
