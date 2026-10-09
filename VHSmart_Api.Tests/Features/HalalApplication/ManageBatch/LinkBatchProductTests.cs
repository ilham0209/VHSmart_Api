using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class LinkBatchProductTests
{
    [Fact]
    public async Task Handle_ValidProduct_InsertsAnActiveRowAndAnswersTheListRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA, name: "Batch Product Halal A");
        var productId = await SeedValidProductAsync(db, CompanyA, name: "Santan Kicap");

        var response = await new LinkBatchProductHandler(db, user)
            .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None);

        var stored = await db.BatchProducts.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(BatchProductMappingStatus.Active, stored.MappingStatus);

        Assert.Equal(stored.Id, response.BatchProductId);
        Assert.Equal(productId, response.ProductId);
        Assert.Equal("Santan Kicap", response.ProductName);
        Assert.Equal("Sereni", response.Brand);
        Assert.Equal("Linked", response.LinkIngredientStatus);
        Assert.Equal(BatchProductMappingStatus.Active, response.MappingStatus);
    }

    [Fact]
    public async Task Handle_AlreadyActiveRow_DoesNotInsertASecondRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedValidProductAsync(db, CompanyA);
        await SeedBatchProductAsync(db, CompanyA, batchId, productId);

        await new LinkBatchProductHandler(db, user)
            .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None);

        Assert.Equal(1, await db.BatchProducts.CountAsync());
    }

    [Fact]
    public async Task Handle_InactiveRow_IsFlippedBackToActive()
    {
        // Database.md 10: link/unlink toggles MappingStatus - one live row per pair.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedValidProductAsync(db, CompanyA);
        await SeedBatchProductAsync(
            db, CompanyA, batchId, productId, BatchProductMappingStatus.Inactive);

        var response = await new LinkBatchProductHandler(db, user)
            .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None);

        var stored = await db.BatchProducts.SingleAsync();
        Assert.Equal(BatchProductMappingStatus.Active, stored.MappingStatus);
        Assert.Equal(BatchProductMappingStatus.Active, response.MappingStatus);
    }

    [Fact]
    public async Task Handle_FoodPremiseBatch_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));
        var productId = await SeedValidProductAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None));

        Assert.Equal("This batch does not accept products.", exception.Message);
        Assert.Empty(await db.BatchProducts.ToArrayAsync());
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedValidProductAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(Guid.NewGuid(), productId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProductOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var foreignProductId = await SeedValidProductAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(batchId, foreignProductId), CancellationToken.None));

        Assert.Empty(await db.BatchProducts.ToArrayAsync());
    }

    // D-18: a product without any linked raw material is not Valid and cannot join a batch.
    [Fact]
    public async Task Handle_ProductWithoutIngredient_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedUnlinkedProductAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None));

        Assert.Equal("Product has no linked ingredient.", exception.Message);
    }

    // D-18 / Q10 default: an expired raw material certificate BLOCKS the link.
    [Fact]
    public async Task Handle_ProductWithExpiredCertificate_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedExpiredProductAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None));

        Assert.Equal(
            "Product has raw materials that are not halal or have expired.",
            exception.Message);
        Assert.Empty(await db.BatchProducts.ToArrayAsync());
    }

    // D-18: a linked raw material WITHOUT a halal certificate at all is not Valid either.
    [Fact]
    public async Task Handle_ProductWithUncertifiedIngredient_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var (productId, _) = await VerifyHalalTestData.SeedProductWithIngredientAsync(
            db, "Santan Tepung Goreng", CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchProductHandler(db, user)
                .Handle(new LinkBatchProductCommand(batchId, productId), CancellationToken.None));

        Assert.Equal(
            "Product has raw materials that are not halal or have expired.",
            exception.Message);
    }

    [Fact]
    public async Task Validator_MissingProductId_Fails()
    {
        var validator = new LinkBatchProductValidator();

        var result = await validator.ValidateAsync(
            new LinkBatchProductCommand(Guid.NewGuid(), Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Product is required.");
    }
}
