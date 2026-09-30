using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class DeleteCertificationBodyTests
{
    private static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<CertificationBodyEntity> SeedRowAsync(
        TestableVHSmartDbContext db,
        string name)
    {
        var malaysiaId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var row = new CertificationBodyEntity { Name = name, CountryId = malaysiaId };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletes()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, "JAKIM");

        await new DeleteCertificationBodyHandler(db, user)
            .Handle(new DeleteCertificationBodyCommand(row.Id), CancellationToken.None);

        var stored = await db.CertificationBodies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == row.Id);
        Assert.True(stored.IsDeleted);

        var live = await db.CertificationBodies.CountAsync();
        Assert.Equal(0, live);
    }

    [Fact]
    public async Task Handle_DeletedRow_FreesTheName()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, "JAKIM");
        await new DeleteCertificationBodyHandler(db, user)
            .Handle(new DeleteCertificationBodyCommand(row.Id), CancellationToken.None);

        // The duplicate check only sees live rows, so the name can be reused after a delete.
        var malaysiaId = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var response = await new CreateCertificationBodyHandler(db, user)
            .Handle(
                new CreateCertificationBodyCommand(
                    "JAKIM", null, null, null, null, null, null, null,
                    malaysiaId, null, null, null, null, null, null, null, null),
                CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteCertificationBodyHandler(db, user)
                .Handle(new DeleteCertificationBodyCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid()));
        var companyUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new DeleteCertificationBodyHandler(db, companyUser)
                .Handle(new DeleteCertificationBodyCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
