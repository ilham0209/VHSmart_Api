using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetApplicationProductsTests
{
    [Fact]
    public async Task Handle_ActiveBatchProducts_AreReturnedWithBrandAndIngredients()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);

        var brandId = await BatchTestData.SeedBrandAsync(
            db, name: "Sereni", companyId: CompanyA);
        var productId = await ProductTestData.SeedProductAsync(
            db, name: "Sereni Kicap Manis", companyId: CompanyA, brandId: brandId);

        // Two ingredients - the spec's comma separated Ingredients cell, alphabetical.
        var saltId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Salt", companyId: CompanyA);
        var flourId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Rice Flour", companyId: CompanyA);
        await ProductTestData.SeedIngredientAsync(db, productId, saltId, companyId: CompanyA);
        await ProductTestData.SeedIngredientAsync(db, productId, flourId, companyId: CompanyA);

        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationProductsHandler(db, user)
            .Handle(new GetApplicationProductsQuery(applicationId), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(productId, row.ProductId);
        Assert.Equal("Sereni Kicap Manis", row.ProductName);
        Assert.Equal("Sereni", row.Brand);
        Assert.Equal("Rice Flour, Salt", row.Ingredients);
    }

    [Fact]
    public async Task Handle_InactiveBatchProductLink_IsHidden()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await ProductTestData.SeedProductAsync(
            db, companyId: CompanyA);
        await BatchTestData.SeedBatchProductAsync(
            db, CompanyA, batchId, productId,
            mappingStatus: BatchProductMappingStatus.Inactive);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationProductsHandler(db, user)
            .Handle(new GetApplicationProductsQuery(applicationId), CancellationToken.None);

        Assert.Empty(rows);
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

        var rows = await new GetApplicationProductsHandler(db, user)
            .Handle(new GetApplicationProductsQuery(applicationId), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(string.Empty, row.Ingredients);
    }

    [Fact]
    public async Task Handle_ApplicationWithoutBatch_ReturnsEmpty()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var rows = await new GetApplicationProductsHandler(db, user)
            .Handle(new GetApplicationProductsQuery(applicationId), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetApplicationProductsHandler(db, user)
                .Handle(
                    new GetApplicationProductsQuery(foreignId),
                    CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }
}
