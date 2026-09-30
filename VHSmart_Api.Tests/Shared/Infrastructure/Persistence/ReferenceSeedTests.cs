using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Persistence;

// R-01 seed content: the tables are only useful if the migration really carries the full country
// list, the Malaysian states and the 9 schemes of spec 12.1.
public class ReferenceSeedTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task EnsureCreated_SeedsFullIsoCountryList()
    {
        var db = await CreateSeededDatabaseAsync();

        var countries = await db.Countries.ToListAsync();

        // ISO 3166-1 alpha-3 carries 249 codes - Database.md 14 asks for the full list.
        Assert.Equal(249, countries.Count);
        Assert.Equal(countries.Count, countries.Select(country => country.IsoCode).Distinct().Count());
        Assert.All(countries, country =>
        {
            Assert.False(string.IsNullOrWhiteSpace(country.Name));
            Assert.Equal(3, country.IsoCode.Length);
        });
        Assert.Contains(countries, country => country.IsoCode == "MYS" && country.Name == "Malaysia");
        Assert.All(countries, country => Assert.Equal("system", country.SysUserCreated));
    }

    [Fact]
    public async Task EnsureCreated_SeedsSixteenMalaysianStatesUnderMalaysia()
    {
        var db = await CreateSeededDatabaseAsync();

        var malaysia = await db.Countries.SingleAsync(country => country.IsoCode == "MYS");
        var states = await db.States.ToListAsync();

        Assert.Equal(ReferenceSeedData.MalaysiaCountryId, malaysia.Id);
        // 13 states + 3 federal territories; no other country has states (spec 7.1 keeps a
        // free-text state for them).
        Assert.Equal(16, states.Count);
        Assert.All(states, state => Assert.Equal(malaysia.Id, state.CountryId));
        Assert.Contains(states, state => state.Name == "Selangor");
        Assert.Contains(states, state => state.Name == "Pulau Pinang");
        Assert.Contains(states, state => state.Name == "Kuala Lumpur");
        Assert.Contains(states, state => state.Name == "Putrajaya");
    }

    [Fact]
    public async Task EnsureCreated_SeedsNineSchemesInPickerOrder()
    {
        var db = await CreateSeededDatabaseAsync();

        var schemes = await db.Schemes.OrderBy(scheme => scheme.SortOrder).ToListAsync();

        Assert.Equal(9, schemes.Count);
        Assert.Equal(
            new[] { "PR", "PM", null, "BG", "FM", "PL", "KO", "MD", "OEM" },
            schemes.Select(scheme => scheme.Code).ToArray());
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            schemes.Select(scheme => scheme.SortOrder).ToArray());
        // IsFoodPremise is true for Food Premises only (Database.md 3).
        Assert.Equal("Food Premises", Assert.Single(schemes, scheme => scheme.IsFoodPremise).Name);
        // The Abattoirs code was never seen, so the seed leaves it null (Database.md 3, VERIFY).
        Assert.Null(Assert.Single(schemes, scheme => scheme.Name == "Abattoirs").Code);
        Assert.All(schemes, scheme => Assert.Equal("system", scheme.SysUserCreated));
    }
}
