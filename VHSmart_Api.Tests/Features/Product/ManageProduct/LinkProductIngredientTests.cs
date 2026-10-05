using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class LinkProductIngredientTests
{
    private const string BrandRequiredMessage =
        "Please assign at least one Brand (Manage Brand Information) to retrieve the Ingredient Information";

    [Fact]
    public async Task Handle_NewPair_CreatesAnActiveRowOwnedByTheProductCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db, "Rice Flour");

        var response = await new LinkProductIngredientHandler(db, user)
            .Handle(new LinkProductIngredientCommand(productId, rawMaterialId),
                CancellationToken.None);

        Assert.Equal(ProductIngredientMappingStatus.Active, response.MappingStatus);
        Assert.Equal("Rice Flour", response.Ingredient);

        var stored = await db.ProductIngredients.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(productId, stored.ProductId);
        Assert.Equal(rawMaterialId, stored.RawMaterialId);
    }

    [Fact]
    public async Task Handle_ExistingUnlinkedRow_FlipsBackToActiveInsteadOfDuplicating()
    {
        // Database.md 9: link/unlink toggles MappingStatus over the one live row the filtered
        // unique index (ProductId, RawMaterialId) allows.
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(
            db, productId, rawMaterialId, ProductIngredientMappingStatus.Inactive);

        var response = await new LinkProductIngredientHandler(db, user)
            .Handle(new LinkProductIngredientCommand(productId, rawMaterialId),
                CancellationToken.None);

        Assert.Equal(ProductIngredientMappingStatus.Active, response.MappingStatus);
        var stored = await db.ProductIngredients.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
        Assert.Equal(ProductIngredientMappingStatus.Active, stored.MappingStatus);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedPair_IsIdempotent()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var first = await new LinkProductIngredientHandler(db, user)
            .Handle(new LinkProductIngredientCommand(productId, rawMaterialId),
                CancellationToken.None);
        var second = await new LinkProductIngredientHandler(db, user)
            .Handle(new LinkProductIngredientCommand(productId, rawMaterialId),
                CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.ProductIngredients.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Handle_ForeignUnsharedRawMaterial_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var foreignMaterialId =
            await SeedRawMaterialAsync(db, "Foreign Sugar", companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkProductIngredientHandler(db, user)
                .Handle(new LinkProductIngredientCommand(productId, foreignMaterialId),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownRawMaterial_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkProductIngredientHandler(db, user)
                .Handle(new LinkProductIngredientCommand(productId, Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyBrandAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkProductIngredientHandler(db, user)
                .Handle(new LinkProductIngredientCommand(Guid.NewGuid(), rawMaterialId),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProductOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserB();
        var db = await CreateDbAsync(user);
        var foreignProductId = await SeedProductAsync(db, companyId: CompanyA);
        var rawMaterialId = await SeedRawMaterialAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkProductIngredientHandler(db, user)
                .Handle(new LinkProductIngredientCommand(foreignProductId, rawMaterialId),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyWithoutBrandLink_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkProductIngredientHandler(db, user)
                .Handle(new LinkProductIngredientCommand(productId, rawMaterialId),
                    CancellationToken.None));

        Assert.Equal(BrandRequiredMessage, exception.Message);
    }

    [Fact]
    public async Task Validator_MissingRawMaterial_Fails()
    {
        var validator = new LinkProductIngredientValidator();

        var result = await validator.ValidateAsync(
            new LinkProductIngredientCommand(Guid.NewGuid(), null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Raw material is required.");
    }
}
