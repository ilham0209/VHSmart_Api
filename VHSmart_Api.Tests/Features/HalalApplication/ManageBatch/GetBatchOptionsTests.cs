using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class GetBatchOptionsTests
{
    [Fact]
    public async Task Handle_Schemes_ComeFromTheSeededGlobalListInSortOrder()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await new GetBatchOptionsHandler(db, user)
            .Handle(new GetBatchOptionsQuery(), CancellationToken.None);

        var expected = await db.Schemes.AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .Select(row => row.Id)
            .ToListAsync();
        Assert.Equal(expected, response.Schemes.Select(row => row.Id).ToArray());

        // The add modal offers every scheme incl. Food Premises (spec 12.2 flow 3).
        var foodPremiseId = await FoodPremiseSchemeIdAsync(db);
        Assert.Contains(response.Schemes, option => option.Id == foodPremiseId);
    }

    [Fact]
    public async Task Handle_Brands_AreOnlyTheCallersOwnCompanyBrandRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var alpha = await SeedBrandAsync(db, name: "Alpha Brand");
        var beta = await SeedBrandAsync(db, name: "Beta Brand");
        await SeedBrandAsync(db, name: "Foreign Brand", companyId: CompanyB);

        var response = await new GetBatchOptionsHandler(db, user)
            .Handle(new GetBatchOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { alpha, beta },
            response.Brands.Select(row => row.Id).ToArray());
        Assert.Equal(
            new[] { "Alpha Brand", "Beta Brand" },
            response.Brands.Select(row => row.Name).ToArray());
        Assert.DoesNotContain(response.Brands, row => row.Name == "Foreign Brand");
    }

    [Fact]
    public async Task Handle_Manufacturers_ExcludeSupplierOnlyAndForeignRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var manufacturerId = await SeedManufacturerAsync(db, name: "Santan Foods");
        await SeedSupplierOnlyAsync(db, name: "Santan Trading");
        await SeedManufacturerAsync(db, companyId: CompanyB, name: "Foreign Foods");

        var response = await new GetBatchOptionsHandler(db, user)
            .Handle(new GetBatchOptionsQuery(), CancellationToken.None);

        var option = Assert.Single(response.Manufacturers);
        Assert.Equal(manufacturerId, option.Id);
        Assert.Equal("Santan Foods", option.Name);
    }

    [Fact]
    public async Task Handle_NoOwnRows_ReturnsEmptyListsAndStillTheSeededSchemes()
    {
        var user = UserB();
        var db = await CreateDbAsync(user);
        await SeedBrandAsync(db, name: "Alpha Brand", companyId: CompanyA);

        var response = await new GetBatchOptionsHandler(db, user)
            .Handle(new GetBatchOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Brands);
        Assert.Empty(response.Manufacturers);
        Assert.NotEmpty(response.Schemes);
    }
}
