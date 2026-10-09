using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetApplicationRawMaterialsTests
{
    [Fact]
    public async Task Handle_DistinctMaterials_AreListedWithTheirProductsAndCertificate()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);

        // One material behind two batch products -> one row, both products named.
        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Rice Flour", companyId: CompanyA);
        await ProductTestData.SeedHalalCertificateAsync(
            db,
            rawMaterialId,
            expiryDate: DateTime.UtcNow.AddDays(30),
            referenceNo: "JAKIM/1/2026/0001",
            authority: "JAKIM",
            companyId: CompanyA);

        var firstProduct = await ProductTestData.SeedProductAsync(
            db, name: "Bihun Goreng", companyId: CompanyA);
        var secondProduct = await ProductTestData.SeedProductAsync(
            db, name: "Nasi Impit", companyId: CompanyA);
        await ProductTestData.SeedIngredientAsync(
            db, firstProduct, rawMaterialId, companyId: CompanyA);
        await ProductTestData.SeedIngredientAsync(
            db, secondProduct, rawMaterialId, companyId: CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, firstProduct);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, secondProduct);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationRawMaterialsHandler(db, user)
            .Handle(
                new GetApplicationRawMaterialsQuery(applicationId),
                CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(rawMaterialId, row.RawMaterialId);
        Assert.Equal("Rice Flour", row.Ingredient);
        Assert.Equal("Bihun Goreng, Nasi Impit", row.Products);
        Assert.Equal("JAKIM/1/2026/0001", row.DocumentReference);
        Assert.Equal("JAKIM", row.HalalCertificateProvider);
        Assert.NotNull(row.ExpiredDate);
        Assert.NotNull(row.ManufacturerName);
        Assert.Equal("Plant Based", row.IngredientSource);
    }

    [Fact]
    public async Task Handle_InactiveIngredientLink_IsNotListed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await ProductTestData.SeedProductAsync(
            db, companyId: CompanyA);
        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, companyId: CompanyA);
        await ProductTestData.SeedIngredientAsync(
            db,
            productId,
            rawMaterialId,
            mappingStatus: ProductIngredientMappingStatus.Inactive,
            companyId: CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationRawMaterialsHandler(db, user)
            .Handle(
                new GetApplicationRawMaterialsQuery(applicationId),
                CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ForeignMaterialLinkedToOwnProduct_IsNotListed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        await SeedCompanyAsync(db, CompanyB, "Borneo Halal Ventures");
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await ProductTestData.SeedProductAsync(
            db, companyId: CompanyA);

        // Company B's material, not shared with A: the RawMaterials visibility filter
        // (CodingRules 7.3) hides it even though the ingredient link is A's own row.
        var foreignMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Foreign Sugar", companyId: CompanyB);
        await ProductTestData.SeedIngredientAsync(
            db, productId, foreignMaterialId, companyId: CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationRawMaterialsHandler(db, user)
            .Handle(
                new GetApplicationRawMaterialsQuery(applicationId),
                CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ApplicationWithoutBatch_ReturnsEmpty()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var rows = await new GetApplicationRawMaterialsHandler(db, user)
            .Handle(
                new GetApplicationRawMaterialsQuery(applicationId),
                CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetApplicationRawMaterialsHandler(db, user)
                .Handle(
                    new GetApplicationRawMaterialsQuery(foreignId),
                    CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }
}
