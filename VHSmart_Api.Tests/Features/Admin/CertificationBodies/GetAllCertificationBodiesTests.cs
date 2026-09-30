using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class GetAllCertificationBodiesTests
{
    private static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

    private static TestCurrentUser CompanyUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid());

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<CertificationBodyEntity> SeedRowAsync(
        TestableVHSmartDbContext db,
        string name,
        string? notes = null)
    {
        var malaysia = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var row = new CertificationBodyEntity
        {
            Name = name,
            CountryId = malaysia,
            Notes = notes
        };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(CompanyUser());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetAllCertificationBodiesHandler(db, CompanyUser())
                .Handle(new GetAllCertificationBodiesQuery(), CancellationToken.None));

        Assert.Contains("platform administrator", exception.Message);
    }

    [Fact]
    public async Task Handle_List_ReturnsCountryDisplayName()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        await SeedRowAsync(db, "JAKIM", notes: "National body");

        var result = await new GetAllCertificationBodiesHandler(db, user)
            .Handle(new GetAllCertificationBodiesQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal("JAKIM", row.Name);
        Assert.Equal("Malaysia", row.CountryName);
        Assert.Equal("National body", row.Notes);
        Assert.Null(row.ModifiedDate);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsNameOrNotes()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        await SeedRowAsync(db, "JAKIM");
        await SeedRowAsync(db, "Foreign Body", notes: "accredited by ILAC");

        var byName = await new GetAllCertificationBodiesHandler(db, user)
            .Handle(
                new GetAllCertificationBodiesQuery { Request = new DataGridRequest { SearchTerm = "JAK" } },
                CancellationToken.None);
        var byNotes = await new GetAllCertificationBodiesHandler(db, user)
            .Handle(
                new GetAllCertificationBodiesQuery { Request = new DataGridRequest { SearchTerm = "ILAC" } },
                CancellationToken.None);

        Assert.Equal("JAKIM", Assert.Single(byName.Data).Name);
        Assert.Equal("Foreign Body", Assert.Single(byNotes.Data).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = PlatformUser();
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, "Doomed Body");
        db.CertificationBodies.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllCertificationBodiesHandler(db, user)
            .Handle(new GetAllCertificationBodiesQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }
}
