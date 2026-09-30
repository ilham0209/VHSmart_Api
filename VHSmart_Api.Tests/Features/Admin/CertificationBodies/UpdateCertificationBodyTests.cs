using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class UpdateCertificationBodyTests
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

    private static async Task<CertificationBodyEntity> SeedRowAsync(
        TestableVHSmartDbContext db,
        string name)
    {
        var row = new CertificationBodyEntity
        {
            Name = name,
            CountryId = await MalaysiaIdAsync(db),
            City = "Kuala Lumpur",
            Postcode = "50450",
            State = "KL",
            Telephone = "03",
            Email = "cb@example.com",
            ContactPerson = "Contact"
        };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static UpdateCertificationBodyCommand Command(
        Guid id,
        Guid countryId,
        string name) =>
        new(
            id, name, null, null, null,
            "Jalan Baru", null, "Shah Alam", "40000",
            countryId, "Selangor", "0377777777", null, null,
            "new@example.com", "New Contact", null, null);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesRow()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, "JAKIM");
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new UpdateCertificationBodyHandler(db, user)
            .Handle(Command(row.Id, malaysiaId, "JAKIM Malaysia"), CancellationToken.None);

        Assert.Equal("JAKIM Malaysia", response.Name);

        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.Equal("Shah Alam", stored.City);
        Assert.Equal("new@example.com", stored.Email);
        Assert.Equal(malaysiaId, stored.CountryId);
    }

    [Fact]
    public async Task Handle_DuplicateOfAnotherRow_ThrowsConflict()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        await SeedRowAsync(db, "JAKIM");
        var second = await SeedRowAsync(db, "MyCC");
        var malaysiaId = await MalaysiaIdAsync(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCertificationBodyHandler(db, user)
                .Handle(Command(second.Id, malaysiaId, "JAKIM"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateOfItself_IsAllowed()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, "JAKIM");
        var malaysiaId = await MalaysiaIdAsync(db);

        var response = await new UpdateCertificationBodyHandler(db, user)
            .Handle(Command(row.Id, malaysiaId, "JAKIM"), CancellationToken.None);

        Assert.Equal("JAKIM", response.Name);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var malaysiaId = await MalaysiaIdAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCertificationBodyHandler(db, user)
                .Handle(Command(Guid.NewGuid(), malaysiaId, "JAKIM"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new UpdateCertificationBodyHandler(db, companyUser)
                .Handle(Command(Guid.NewGuid(), Guid.NewGuid(), "JAKIM"), CancellationToken.None));
    }
}
