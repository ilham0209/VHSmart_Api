using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Lookups;

namespace VHSmart_Api.Tests.Features.Admin.Lookups;

public class GetSchemesTests
{
    private static async Task<TestableVHSmartDbContext> CreateSeededDatabaseAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_ReturnsNineSchemes_InSortOrder()
    {
        var db = await CreateSeededDatabaseAsync();

        var result = await new GetSchemesHandler(db)
            .Handle(new GetSchemesQuery(), CancellationToken.None);

        Assert.Equal(9, result.Count);
        Assert.Equal(
            new[] { "PR", "PM", null, "BG", "FM", "PL", "KO", "MD", "OEM" },
            result.Select(scheme => scheme.Code).ToArray());
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            result.Select(scheme => scheme.SortOrder).ToArray());
        Assert.Equal("Food Premises", Assert.Single(result, scheme => scheme.IsFoodPremise).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedScheme_IsNotListed()
    {
        var db = await CreateSeededDatabaseAsync();
        var logistics = await db.Schemes.SingleAsync(scheme => scheme.Name == "Logistics");
        db.Schemes.Remove(logistics);
        await db.SaveChangesAsync();

        var result = await new GetSchemesHandler(db)
            .Handle(new GetSchemesQuery(), CancellationToken.None);

        Assert.Equal(8, result.Count);
        Assert.DoesNotContain(result, scheme => scheme.Name == "Logistics");
        // The remaining rows keep their sort order - no renumbering on delete.
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 7, 8, 9 },
            result.Select(scheme => scheme.SortOrder).ToArray());
    }
}
