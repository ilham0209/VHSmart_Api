using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class UnlinkProductIngredientTests
{
    private const string BrandRequiredMessage =
        "Please assign at least one Brand (Manage Brand Information) to retrieve the Ingredient Information";

    [Fact]
    public async Task Handle_ActiveRow_TogglesToUnlinkedAndStaysOnTheTable()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        var ingredientId = await SeedIngredientAsync(db, productId, rawMaterialId);

        await new UnlinkProductIngredientHandler(db, user)
            .Handle(new UnlinkProductIngredientCommand(productId, ingredientId),
                CancellationToken.None);

        // Database.md 9: the pair is kept and shown with its status - not a physical delete
        // and not a soft delete either, so a later link just flips it back.
        var stored = await db.ProductIngredients.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
        Assert.Equal(ProductIngredientMappingStatus.Inactive, stored.MappingStatus);
    }

    [Fact]
    public async Task Handle_AlreadyUnlinkedRow_StillSucceeds()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var ingredientId = await SeedIngredientAsync(
            db,
            productId,
            await SeedRawMaterialAsync(db),
            ProductIngredientMappingStatus.Inactive);

        await new UnlinkProductIngredientHandler(db, user)
            .Handle(new UnlinkProductIngredientCommand(productId, ingredientId),
                CancellationToken.None);

        var stored = await db.ProductIngredients.SingleAsync();
        Assert.Equal(ProductIngredientMappingStatus.Inactive, stored.MappingStatus);
    }

    [Fact]
    public async Task Handle_IngredientOfAnotherProduct_ThrowsNotFound()
    {
        // The ingredient id is scoped to the product in the route, so it can never unlink
        // somebody else's row (404, never 403).
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var otherProductId = await SeedProductAsync(db, name: "Other Product", code: "PRD-002");
        var otherIngredientId = await SeedIngredientAsync(
            db, otherProductId, await SeedRawMaterialAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkProductIngredientHandler(db, user)
                .Handle(new UnlinkProductIngredientCommand(productId, otherIngredientId),
                    CancellationToken.None));

        Assert.Equal(
            ProductIngredientMappingStatus.Active,
            (await db.ProductIngredients.SingleAsync()).MappingStatus);
    }

    [Fact]
    public async Task Handle_UnknownIngredient_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkProductIngredientHandler(db, user)
                .Handle(new UnlinkProductIngredientCommand(productId, Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkProductIngredientHandler(db, user)
                .Handle(new UnlinkProductIngredientCommand(Guid.NewGuid(), Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyWithoutBrandLink_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var ingredientId = await SeedIngredientAsync(
            db, productId, await SeedRawMaterialAsync(db));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new UnlinkProductIngredientHandler(db, user)
                .Handle(new UnlinkProductIngredientCommand(productId, ingredientId),
                    CancellationToken.None));

        Assert.Equal(BrandRequiredMessage, exception.Message);
    }

    [Fact]
    public async Task Validator_MissingIngredientId_Fails()
    {
        var validator = new UnlinkProductIngredientValidator();

        var result = await validator.ValidateAsync(
            new UnlinkProductIngredientCommand(Guid.NewGuid(), Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Ingredient is required.");
    }
}
