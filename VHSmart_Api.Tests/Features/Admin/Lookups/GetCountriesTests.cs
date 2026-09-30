using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Lookups;

namespace VHSmart_Api.Tests.Features.Admin.Lookups;

public class GetCountriesTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_ReturnsEverySeededCountry_OrderedByName()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetCountriesHandler(db)
            .Handle(new GetCountriesQuery(), CancellationToken.None);

        Assert.Equal(249, result.Count);
        Assert.Contains(result, country => country.IsoCode == "MYS" && country.Name == "Malaysia");

        var names = result.Select(country => country.Name).ToList();
        Assert.Equal(names.OrderBy(name => name), names);
    }

    [Fact]
    public async Task Handle_SoftDeletedCountry_IsNotListed()
    {
        var db = await CreateSeededDatabaseAsync();
        var malaysia = await db.Countries.SingleAsync(country => country.IsoCode == "MYS");
        db.Countries.Remove(malaysia);
        await db.SaveChangesAsync();

        var result = await new GetCountriesHandler(db)
            .Handle(new GetCountriesQuery(), CancellationToken.None);

        Assert.Equal(248, result.Count);
        Assert.DoesNotContain(result, country => country.IsoCode == "MYS");
    }
}
