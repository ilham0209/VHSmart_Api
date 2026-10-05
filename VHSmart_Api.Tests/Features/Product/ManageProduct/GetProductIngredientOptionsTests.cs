using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class GetProductIngredientOptionsTests
{
    private const string BrandRequiredMessage =
        "Please assign at least one Brand (Manage Brand Information) to retrieve the Ingredient Information";

    [Fact]
    public async Task Handle_WithNoLink_ReturnsEveryVisibleMaterial()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(db, "Rice Flour");
        await SeedRawMaterialAsync(db, "Cane Sugar");

        var result = await QueryAsync(db, productId);

        Assert.Equal(
            new[] { "Cane Sugar", "Rice Flour" },
            result.Data.Select(row => row.Ingredient).ToArray());
        Assert.Equal(2, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_LinkedAndUnlinkedMaterials_AreNotOffered()
    {
        // Both a live row and an UNLINKED one are already on the tab with their own Action
        // icon, so neither belongs in the "+ Add New Ingredient" source (spec 9.1).
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var activeId = await SeedRawMaterialAsync(db, "Rice Flour");
        var inactiveId = await SeedRawMaterialAsync(db, "Cane Sugar");
        await SeedIngredientAsync(db, productId, activeId);
        await SeedIngredientAsync(
            db, productId, inactiveId, ProductIngredientMappingStatus.Inactive);
        await SeedRawMaterialAsync(db, "Malt Extract");

        var result = await QueryAsync(db, productId);

        Assert.Equal(new[] { "Malt Extract" }, result.Data.Select(row => row.Ingredient));
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsIngredientOrCode()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(db, "Rice Flour");
        var codedId = await SeedRawMaterialAsync(db, "Cane Sugar");
        var code = await db.RawMaterials
            .AsNoTracking()
            .Where(row => row.Id == codedId)
            .Select(row => row.IngredientCode)
            .SingleAsync();

        var byName = await QueryAsync(db, productId, "cane");
        var byCode = await QueryAsync(db, productId, code);

        Assert.Equal("Cane Sugar", Assert.Single(byName.Data).Ingredient);
        Assert.Equal("Cane Sugar", Assert.Single(byCode.Data).Ingredient);
    }

    [Fact]
    public async Task Handle_ForeignUnsharedMaterial_IsNotOffered()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(db, "Foreign Sugar", companyId: CompanyB);

        var result = await QueryAsync(db, productId);

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Handle_SharedMaterial_IsOffered()
    {
        // "Accessible For" of CodingRules 7.3: a material another company shared with the
        // caller may be linked to the caller's product.
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(
            db, "Shared Sugar", companyId: CompanyB, accessibleCompanyIds: [CompanyA]);

        var result = await QueryAsync(db, productId);

        Assert.Equal("Shared Sugar", Assert.Single(result.Data).Ingredient);
    }

    [Fact]
    public async Task Handle_MaterialWithCertificate_AnswersItsStatusAfterPaging()
    {
        // Derived data is never searched or sorted on (RM-02 stance): the status is only
        // painted over the rows that survived paging.
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db, "Rice Flour");
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2030, 6, 30));

        var result = await QueryAsync(db, productId);

        Assert.Equal(
            HalalStatus.Valid,
            Assert.Single(result.Data).HalalCertificateStatus);
    }

    [Fact]
    public async Task Handle_MaterialWithoutCertificate_AnswersNullStatus()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(db);

        var result = await QueryAsync(db, productId);

        Assert.Null(Assert.Single(result.Data).HalalCertificateStatus);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => QueryAsync(db, Guid.NewGuid()));
    }

    [Fact]
    public async Task Handle_ProductOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserB());
        var foreignId = await SeedProductAsync(db, companyId: CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(
            () => QueryAsync(db, foreignId, user: UserB()));
    }

    [Fact]
    public async Task Handle_CompanyWithoutBrandLink_ThrowsBusinessRule()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedProductAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => QueryAsync(db, productId));

        Assert.Equal(BrandRequiredMessage, exception.Message);
    }

    private static Task<DataGridResponse<ProductIngredientOptionResponse>> QueryAsync(
        TestableVHSmartDbContext db,
        Guid productId,
        string? term = null,
        ICurrentUser? user = null) =>
        new GetProductIngredientOptionsHandler(db, user ?? UserA()).Handle(
            new GetProductIngredientOptionsQuery(productId)
            {
                Request = new DataGridRequest { SearchTerm = term }
            },
            CancellationToken.None);
}
