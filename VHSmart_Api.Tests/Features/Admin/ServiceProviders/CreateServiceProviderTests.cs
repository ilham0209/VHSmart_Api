using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.ServiceProviders;

public class CreateServiceProviderTests
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

    private static CreateServiceProviderCommand Command(Guid countryId, string name = "Santan Provider") =>
        new(
            name, "Halal food supplier", "Jalan Gombak 1", "50450",
            countryId, "Wilayah Persekutuan", "0380000000", null, null,
            "provider@example.com", "Aminah", "CIMB", "8001234567");

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithCompanyIdFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new CreateServiceProviderHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        Assert.Equal("Santan Provider", response.Name);
        Assert.Equal(malaysiaId, response.CountryId);

        var stored = await db.ServiceProviders.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("CIMB", stored.BankName);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        await new CreateServiceProviderHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateServiceProviderHandler(db, user)
                .Handle(Command(malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var malaysiaId = await MalaysiaIdAsync(db);

        var companyARow = await new CreateServiceProviderHandler(db, userA)
            .Handle(Command(malaysiaId, "Shared name"), CancellationToken.None);

        // Company B has no row yet, so its duplicate check must not see company A's row.
        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        var companyBRow = await new CreateServiceProviderHandler(dbB, userB)
            .Handle(Command(malaysiaId, "Shared name"), CancellationToken.None);

        Assert.NotEqual(companyARow.Id, companyBRow.Id);
        Assert.Equal(CompanyB, (await dbB.ServiceProviders.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(new CreateServiceProviderCommand(
            string.Empty, null, string.Empty, string.Empty,
            Guid.Empty, string.Empty, string.Empty, null, null,
            string.Empty, string.Empty, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Address");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Postcode");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "CountryId");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "State");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Telephone");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Email");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ContactPerson");
    }

    [Fact]
    public async Task Validator_UnknownCountry_FailsWithCountryNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CountryId" && failure.ErrorMessage == "Country not found.");
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(
            Command(await MalaysiaIdAsync(db), new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(Command(await MalaysiaIdAsync(db)));

        Assert.True(result.IsValid);
    }
}
