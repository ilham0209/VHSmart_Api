using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.VerifyHalalProductUpdate;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate.VerifyHalalTestData;

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

public class GetVerifyHalalProductsTests
{
    [Fact]
    public async Task Handle_ReturnsTheSpecColumnsWithJoinedBrand()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db, publishStatus: VerifyHalalPublishStatus.Published);

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal("Santan Kicap", row.ProductName);
        Assert.Equal("Sereni", row.Brand);
        Assert.Equal(VerifyHalalPublishStatus.Published, row.PublishStatus);
        Assert.Null(row.HalalApplicationNo);
    }

    [Fact]
    public async Task Handle_ProductWithoutPublishStatus_ReturnsNullStatus()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db);

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        Assert.Null(Assert.Single(result.Data).PublishStatus);
    }

    [Fact]
    public async Task Handle_ForeignProduct_IsInvisible()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db, companyId: CompanyB);

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Handle_SoftDeletedProduct_IsInvisible()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductAsync(db);
        var row = await db.Products.SingleAsync(p => p.Id == id);
        row.IsDeleted = true;
        await db.SaveChangesAsync();

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsProductName()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db, name: "Santan Kicap");
        await SeedProductAsync(db, name: "Santan Sos", code: "PRD-002");

        var result = await new GetVerifyHalalProductsHandler(db, UserA()).Handle(
            new GetVerifyHalalProductsQuery
            {
                Request = new DataGridRequest { SearchTerm = "sos" }
            },
            CancellationToken.None);

        Assert.Equal("Santan Sos", Assert.Single(result.Data).ProductName);
    }

    [Fact]
    public async Task Handle_CategoryFilter_ReturnsOnlyMatchingProducts()
    {
        var db = await CreateDbAsync(UserA());
        var sauces = await SeedProductCategoryAsync(db, "Sauces");
        var beverages = await SeedProductCategoryAsync(db, "Beverages");
        await SeedProductAsync(db, name: "Kicap", categoryId: sauces);
        await SeedProductAsync(db, name: "Teh", categoryId: beverages, code: "PRD-002");

        var result = await new GetVerifyHalalProductsHandler(db, UserA()).Handle(
            new GetVerifyHalalProductsQuery { CategoryId = sauces },
            CancellationToken.None);

        Assert.Equal("Kicap", Assert.Single(result.Data).ProductName);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsProductNameAscending()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db, name: "Zebra Sauce");
        await SeedProductAsync(db, name: "Alpha Sauce", code: "PRD-002");

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Sauce", "Zebra Sauce" },
            result.Data.Select(row => row.ProductName).ToArray());
    }

    [Fact]
    public async Task Handle_HalalExpiryDate_IsDerivedFromIngredientCertificate()
    {
        var db = await CreateDbAsync(UserA());
        var (productId, rawMaterialId) = await SeedProductWithIngredientAsync(db);
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2027, 6, 30));

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal(productId, row.Id);
        Assert.Equal(new DateOnly(2027, 6, 30), row.HalalExpiryDate);
    }

    [Fact]
    public async Task Handle_NoIngredientCertificate_ReturnsNullExpiry()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductAsync(db);

        var result = await new GetVerifyHalalProductsHandler(db, UserA())
            .Handle(new GetVerifyHalalProductsQuery(), CancellationToken.None);

        Assert.Null(Assert.Single(result.Data).HalalExpiryDate);
    }

    [Fact]
    public async Task Handle_ClientSortByBrand_Descending()
    {
        var db = await CreateDbAsync(UserA());
        var alphaBrand = await SeedBrandAsync(db, "Alpha Brand");
        var zuluBrand = await SeedBrandAsync(db, "Zulu Brand");
        await SeedProductAsync(db, name: "First", brandId: alphaBrand);
        await SeedProductAsync(db, name: "Second", brandId: zuluBrand, code: "PRD-002");

        var result = await new GetVerifyHalalProductsHandler(db, UserA()).Handle(
            new GetVerifyHalalProductsQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = nameof(GetVerifyHalalProductsResponse.Brand),
                    SortDescending = true
                }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Zulu Brand", "Alpha Brand" },
            result.Data.Select(row => row.Brand).ToArray());
    }
}
