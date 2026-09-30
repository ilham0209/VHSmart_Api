using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.ServiceProviders;

public class UpdateServiceProviderTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

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

    private static ServiceProviderEntity NewRow(Guid companyId, string name) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = "provider@example.com",
            ContactPerson = "Aminah"
        };

    private static UpdateServiceProviderCommand Command(
        Guid id,
        Guid countryId,
        string name = "Santan Provider") =>
        new(
            id, name, "Updated payee", "Jalan Gombak 2", "53000",
            countryId, "Kuala Lumpur", "0380000000", null, null,
            "new@example.com", "Fatimah", "MAYBANK", "5001234567");

    [Fact]
    public async Task Handle_ValidCommand_UpdatesRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var row = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new UpdateServiceProviderHandler(db, user)
            .Handle(Command(row.Id, malaysiaId, "Santan Supplies"), CancellationToken.None);

        Assert.Equal("Santan Supplies", response.Name);

        var stored = await db.ServiceProviders.AsNoTracking().SingleAsync();
        Assert.Equal("Jalan Gombak 2", stored.Address);
        Assert.Equal("MAYBANK", stored.BankName);
        Assert.Equal(CompanyA, stored.CompanyId);
    }

    [Fact]
    public async Task Handle_DuplicateNameExcludingSelf_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);
        var self = NewRow(CompanyA, "Santan Provider");
        var other = NewRow(CompanyA, "Maybank");
        db.ServiceProviders.AddRange(self, other);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateServiceProviderHandler(db, user)
                .Handle(Command(self.Id, malaysiaId, "Maybank"), CancellationToken.None));

        // Keeping its own name is not a duplicate.
        var unchanged = await new UpdateServiceProviderHandler(db, user)
            .Handle(Command(self.Id, malaysiaId, "Santan Provider"), CancellationToken.None);
        Assert.Equal("Santan Provider", unchanged.Name);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateServiceProviderHandler(db, user)
                .Handle(Command(Guid.NewGuid(), malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B payee");
        db.ServiceProviders.Add(foreignRow);
        await db.SaveChangesAsync();
        var malaysiaId = await MalaysiaIdAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateServiceProviderHandler(db, userA)
                .Handle(Command(foreignRow.Id, malaysiaId), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(new UpdateServiceProviderCommand(
            Guid.Empty, string.Empty, null, string.Empty, string.Empty,
            Guid.Empty, string.Empty, string.Empty, null, null,
            string.Empty, string.Empty, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
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
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateServiceProviderValidator(db);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), await MalaysiaIdAsync(db)));

        Assert.True(result.IsValid);
    }
}
