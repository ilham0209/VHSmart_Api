using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class GetProductOptionsTests
{
    [Fact]
    public async Task Handle_Schemes_AreTheSeededListInPickerOrder()
    {
        var db = await CreateDbAsync(UserA());

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        // HasData seed (Database.md 14): the whole scheme picker, ordered by SortOrder.
        Assert.NotEmpty(response.Schemes);
        Assert.Equal(
            await db.Schemes.AsNoTracking()
                .OrderBy(row => row.SortOrder)
                .Select(row => row.Name)
                .ToListAsync(),
            response.Schemes.Select(row => row.Name).ToList());
    }

    [Fact]
    public async Task Handle_Brands_AreOwnCompanyBrandRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedBrandAsync(db, "Alpha");
        await SeedBrandAsync(db, "Beta");
        // Another company's row and a row of the wrong dropdown must not appear.
        await SeedBrandAsync(db, "Foreign", CompanyB);
        await SeedProductCategoryAsync(db, "Not a brand");

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha", "Beta" },
            response.Brands.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_Categories_AreOwnCompanyProductCategoryRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductCategoryAsync(db, "Sauces");
        await SeedProductCategoryAsync(db, "Beverages");
        await SeedProductCategoryAsync(db, "Foreign", CompanyB);
        await SeedBrandAsync(db, "Not a category");

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Beverages", "Sauces" },
            response.Categories.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_MarketingMethods_AreOwnCompanyRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedMarketingMethodAsync(db, "Verify Halal");
        await SeedMarketingMethodAsync(db, "Self Declare");
        await SeedMarketingMethodAsync(db, "Foreign", CompanyB);

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Self Declare", "Verify Halal" },
            response.MarketingMethods.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_Manufacturers_ListOwnManufacturerRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedManufacturerAsync(db, name: "Santan Foods");
        // A supplier-only row has no manufacturer half and cannot name a product
        // manufacturer; a foreign company's row is outside the tenant filter.
        await SeedSupplierOnlyAsync(db);
        await SeedManufacturerAsync(db, companyId: CompanyB, name: "Foreign Foods");

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        Assert.Equal("Santan Foods", Assert.Single(response.Manufacturers).Name);
    }

    [Fact]
    public async Task Handle_EmptyCompany_AnswersEmptyLists()
    {
        var db = await CreateDbAsync(UserA());

        var response = await new GetProductOptionsHandler(db, UserA())
            .Handle(new GetProductOptionsQuery(), CancellationToken.None);

        Assert.NotEmpty(response.Schemes);
        Assert.Empty(response.Manufacturers);
        Assert.Empty(response.Brands);
        Assert.Empty(response.Categories);
        Assert.Empty(response.MarketingMethods);
    }
}
