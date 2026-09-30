using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Lookups;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Tests.Features.Admin.Lookups;

public class GetStatesTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_CountryFilter_ReturnsOnlyThatCountriesStates()
    {
        var db = await CreateSeededDatabaseAsync();
        // A second country with one state proves the filter really excludes other countries.
        var unitedStates = await db.Countries.SingleAsync(country => country.IsoCode == "USA");
        db.States.Add(new StateEntity { CountryId = unitedStates.Id, Name = "California" });
        await db.SaveChangesAsync();
        var malaysia = await db.Countries.SingleAsync(country => country.IsoCode == "MYS");

        var malaysianStates = await new GetStatesHandler(db)
            .Handle(new GetStatesQuery(malaysia.Id), CancellationToken.None);
        var allStates = await new GetStatesHandler(db)
            .Handle(new GetStatesQuery(null), CancellationToken.None);
        var usStates = await new GetStatesHandler(db)
            .Handle(new GetStatesQuery(unitedStates.Id), CancellationToken.None);

        Assert.Equal(16, malaysianStates.Count);
        Assert.All(malaysianStates, state => Assert.Equal(malaysia.Id, state.CountryId));
        Assert.Equal(17, allStates.Count);
        Assert.Equal("California", Assert.Single(usStates).Name);
    }

    [Fact]
    public async Task Handle_UnknownCountry_ReturnsEmptyList()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetStatesHandler(db)
            .Handle(new GetStatesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_SoftDeletedState_IsNotListed()
    {
        var db = await CreateSeededDatabaseAsync();
        var state = await db.States.SingleAsync(candidate => candidate.Name == "Selangor");
        db.States.Remove(state);
        await db.SaveChangesAsync();
        var malaysia = await db.Countries.SingleAsync(country => country.IsoCode == "MYS");

        var result = await new GetStatesHandler(db)
            .Handle(new GetStatesQuery(malaysia.Id), CancellationToken.None);

        Assert.Equal(15, result.Count);
        Assert.DoesNotContain(result, candidate => candidate.Name == "Selangor");
    }
}
