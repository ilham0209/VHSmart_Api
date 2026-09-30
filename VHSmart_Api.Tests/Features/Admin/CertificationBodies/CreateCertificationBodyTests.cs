using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class CreateCertificationBodyTests
{
    private static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

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

    private static CreateCertificationBodyCommand Command(Guid countryId, string name = "JAKIM") =>
        new(
            name, "JAKIM", null, null,
            "Jalan Gombak", null, "Kuala Lumpur", "50450",
            countryId, "Wilayah Persekutuan", "0380000000", null, null,
            "info@jakim.example", "Director", null, null);

    [Fact]
    public async Task Handle_ValidCommand_StoresRowAndReturnsIt()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new CreateCertificationBodyHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        Assert.Equal("JAKIM", response.Name);
        Assert.Equal(malaysiaId, response.CountryId);

        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        Assert.Equal("Kuala Lumpur", stored.City);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        await new CreateCertificationBodyHandler(db, user)
            .Handle(Command(malaysiaId), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateCertificationBodyHandler(db, user)
                .Handle(Command(malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CreateCertificationBodyHandler(db, companyUser)
                .Handle(Command(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(PlatformUser());
        var validator = new CreateCertificationBodyValidator(db);

        var result = await validator.ValidateAsync(new CreateCertificationBodyCommand(
            string.Empty, null, null, null, null, null, null, null,
            Guid.Empty, null, null, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "City");
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
        var db = await CreateDbAsync(PlatformUser());
        var validator = new CreateCertificationBodyValidator(db);

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CountryId" && failure.ErrorMessage == "Country not found.");
    }

    [Fact]
    public async Task Validator_UnknownRepresentingCountry_Fails()
    {
        var db = await CreateDbAsync(PlatformUser());
        var validator = new CreateCertificationBodyValidator(db);
        var command = new CreateCertificationBodyCommand(
            "JAKIM", null, null, Guid.NewGuid(), null, null, "Kuala Lumpur", "50450",
            await MalaysiaIdAsync(db), "KL", "03", null, null, "a@b.c", "Contact", null, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Representing country not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(PlatformUser());
        var validator = new CreateCertificationBodyValidator(db);

        var result = await validator.ValidateAsync(Command(await MalaysiaIdAsync(db)));

        Assert.True(result.IsValid);
    }
}
