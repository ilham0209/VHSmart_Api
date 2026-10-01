using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Persistence;

public class SubscriptionPackageSeedTests
{
    [Fact]
    public async Task EnsureCreated_SeedsTheSixSection215Codes()
    {
        var db = await CreateDbAsync();

        var packages = await db.SubscriptionPackages.OrderBy(package => package.Code).ToListAsync();

        Assert.Equal(
            ["ADC", "BSC-MICRO", "EASY-HOME", "LTE", "PLA", "TRL"],
            packages.Select(package => package.Code));
        Assert.All(packages, package =>
        {
            Assert.Equal("system", package.SysUserCreated);
            Assert.False(package.IsDeleted);
        });
    }

    [Fact]
    public async Task EnsureCreated_AdvancedAndLite_CarryTheirLimits()
    {
        var db = await CreateDbAsync();

        var advanced = await db.SubscriptionPackages.SingleAsync(package => package.Code == "ADC");
        Assert.Equal(10, advanced.MaxUsers);
        Assert.Equal(5, advanced.MaxPremises);

        var lite = await db.SubscriptionPackages.SingleAsync(package => package.Code == "LTE");
        Assert.Equal(1, lite.MaxUsers);
        Assert.Equal(1, lite.MaxPremises);
    }

    [Fact]
    public async Task EnsureCreated_OtherPackages_AreUnlimited()
    {
        var db = await CreateDbAsync();

        var unlimited = await db.SubscriptionPackages
            .Where(package => package.Code != "ADC" && package.Code != "LTE")
            .ToListAsync();

        Assert.Equal(4, unlimited.Count);
        Assert.All(unlimited, package =>
        {
            Assert.Null(package.MaxUsers);
            Assert.Null(package.MaxPremises);
        });
    }

    [Fact]
    public void PackageEntities_HaveStableUniqueIds()
    {
        // HasData must generate the same primary key on every machine or the migration diffs.
        var first = SubscriptionPackageSeedData.PackageEntities().ToDictionary(package => package.Code, package => package.Id);
        var second = SubscriptionPackageSeedData.PackageEntities().ToDictionary(package => package.Code, package => package.Id);

        Assert.Equal(first, second);
        Assert.Equal(first.Count, first.Values.Distinct().Count());
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
