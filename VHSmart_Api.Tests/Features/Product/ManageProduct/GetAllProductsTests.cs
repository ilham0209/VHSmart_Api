using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class GetAllProductsTests
{
    [Fact]
    public async Task Handle_ReturnsTheSpecColumnsWithJoinedNames()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db);
        // Any edit stamps SysDateModified (VHSmartDbContext audit rule); a never-edited row
        // answers null, exactly like the raw material list.
        var stored = await db.Products.SingleAsync();
        stored.Code = "PRD-001-A";
        await db.SaveChangesAsync();

        var result = await new GetAllProductsHandler(db, UserA())
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal("Santan Kicap", row.Name);
        Assert.Equal("Sereni", row.Brand);
        Assert.Equal("Santan Foods", row.Manufacturer);
        Assert.NotNull(stored.SysDateModified);
        Assert.Equal(stored.SysDateModified, row.ModifiedDate);
    }

    [Fact]
    public async Task Handle_SupplierOnlyManufacturerRow_ShowsTheSupplierName()
    {
        var db = await CreateDbAsync(UserA());
        // The product was created while the row still had a manufacturer half; the loader
        // falls back so the cell is never empty (the list shows the row's name).
        var supplierId = await SeedSupplierOnlyAsync(db);
        await SeedRowAsync(db, name: "Santan Belacan", manufacturerId: supplierId);

        var result = await new GetAllProductsHandler(db, UserA())
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        Assert.Equal("Santan Supplies", Assert.Single(result.Data).Manufacturer);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsNameOrCode()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db, name: "Santan Kicap", code: "PRD-001");
        await SeedRowAsync(db, name: "Santan Sos", code: "PRD-002");

        var byName = await QueryAsync(db, "sos");
        var byCode = await QueryAsync(db, "PRD-001");

        Assert.Equal("Santan Sos", Assert.Single(byName.Data).Name);
        Assert.Equal("Santan Kicap", Assert.Single(byCode.Data).Name);

        static Task<DataGridResponse<GetAllProductsResponse>> QueryAsync(
            TestableVHSmartDbContext db, string term) =>
            new GetAllProductsHandler(db, UserA()).Handle(
                new GetAllProductsQuery
                {
                    Request = new DataGridRequest { SearchTerm = term }
                },
                CancellationToken.None);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsNameAscending()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db, name: "Zebra Sauce");
        await SeedRowAsync(db, name: "Alpha Sauce", code: "PRD-002");

        var result = await new GetAllProductsHandler(db, UserA())
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Sauce", "Zebra Sauce" },
            result.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_ClientSortByBrand_Descending()
    {
        var db = await CreateDbAsync(UserA());
        var alphaBrand = await SeedBrandAsync(db, "Alpha Brand");
        var zuluBrand = await SeedBrandAsync(db, "Zulu Brand");
        await SeedRowAsync(db, name: "First", brandId: alphaBrand);
        await SeedRowAsync(db, name: "Second", brandId: zuluBrand, code: "PRD-002");

        var result = await new GetAllProductsHandler(db, UserA()).Handle(
            new GetAllProductsQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = nameof(GetAllProductsResponse.Brand),
                    SortDescending = true
                }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Zulu Brand", "Alpha Brand" },
            result.Data.Select(row => row.Brand).ToArray());
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_IsInvisible()
    {
        var db = await CreateDbAsync(UserB());
        await SeedRowAsync(db, companyId: CompanyA);

        var result = await new GetAllProductsHandler(db, UserB())
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_ViewAllToken_SeesOnlyOwnRows()
    {
        // The premise stance (PR-01): a Switch Company = ALL caller keeps the explicit
        // CompanyId guard - products are not shareable, so nothing widens the list. This
        // deliberately differs from the raw material list (flagged in the report).
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        await SeedRowAsync(db, companyId: CompanyA);
        await SeedRowAsync(db, name: "Other Product", companyId: CompanyB);

        var result = await new GetAllProductsHandler(db, viewAll)
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalRecords);
        Assert.Equal("Santan Kicap", Assert.Single(result.Data).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db);
        db.Products.Remove(await db.Products.SingleAsync());
        await db.SaveChangesAsync();

        var result = await new GetAllProductsHandler(db, user)
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
        Assert.False(await db.Products.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == id && !row.IsDeleted));
    }

    // The three derived columns (spec 9.1, D-18): computed per page, never stored.
    [Fact]
    public async Task Handle_DerivedColumns_NoIngredient_AnswersUnlinkedExpiredNull()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db);

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Unlinked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Expired, row.HalalStatus);
        Assert.Null(row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_ValidCertificate_AnswersLinkedValidAndItsExpiry()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db, "Rice Flour");
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2030, 6, 30));
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Linked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Valid, row.HalalStatus);
        Assert.Equal(new DateOnly(2030, 6, 30), row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_ExpiredCertificate_AnswersExpired()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2020, 1, 1));
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Linked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Expired, row.HalalStatus);
        Assert.Equal(new DateOnly(2020, 1, 1), row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_MissingCertificate_AnswersExpiredWithoutDate()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        await SeedIngredientAsync(db, productId, await SeedRawMaterialAsync(db));

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Linked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Expired, row.HalalStatus);
        Assert.Null(row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_TwoMaterials_TakeTheEarliestExpiry()
    {
        // D-18: product expiry = the earliest expiry among its linked raw materials, and one
        // expired certificate makes the whole product Expired.
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        var first = await SeedRawMaterialAsync(db, "Rice Flour");
        var second = await SeedRawMaterialAsync(db, "Cane Sugar");
        await SeedHalalCertificateAsync(db, first, new DateTime(2031, 1, 1));
        await SeedHalalCertificateAsync(db, second, new DateTime(2027, 3, 15));
        await SeedIngredientAsync(db, productId, first);
        await SeedIngredientAsync(db, productId, second);

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Linked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Valid, row.HalalStatus);
        Assert.Equal(new DateOnly(2027, 3, 15), row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_UnlinkedRow_IsNotCounted()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2030, 6, 30));
        await SeedIngredientAsync(
            db, productId, rawMaterialId, ProductIngredientMappingStatus.Inactive);

        var row = Assert.Single((await QueryAsync(db)).Data);

        Assert.Equal(ProductIngredientLinkStatus.Unlinked, row.IngredientLinkStatus);
        Assert.Equal(HalalStatus.Expired, row.HalalStatus);
        Assert.Null(row.ExpiryDate);
    }

    [Fact]
    public async Task Handle_DerivedColumns_NeverTakePartInTheSearch()
    {
        // Stance of the raw material list: the derived value is painted on after paging, so
        // the Search box (name / code / GTIN) cannot find a product by its status.
        var db = await CreateDbAsync(UserA());
        var productId = await SeedRowAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var result = await new GetAllProductsHandler(db, UserA()).Handle(
            new GetAllProductsQuery { Request = new DataGridRequest { SearchTerm = "Linked" } },
            CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    private static Task<DataGridResponse<GetAllProductsResponse>> QueryAsync(
        TestableVHSmartDbContext db) =>
        new GetAllProductsHandler(db, UserA())
            .Handle(new GetAllProductsQuery(), CancellationToken.None);

    // One seeding path for this file: the default fixture row with the overrides each test
    // needs (ProductTestData seeds the dropdown rows on demand).
    private static async Task<Guid> SeedRowAsync(
        TestableVHSmartDbContext db,
        string name = "Santan Kicap",
        string? code = "PRD-001",
        Guid? companyId = null,
        Guid? manufacturerId = null,
        Guid? brandId = null) =>
        await SeedProductAsync(
            db,
            name: name,
            companyId: companyId,
            manufacturerId: manufacturerId,
            brandId: brandId,
            code: code);
}
