using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class DeleteProductTests
{
    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);

        await new DeleteProductHandler(db, user)
            .Handle(new DeleteProductCommand(id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.Products.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.Products.ToArrayAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(foreignId), CancellationToken.None));

        var stored = await db.Products.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_ViewAllToken_CannotDeleteAnotherCompanysRow()
    {
        // The premise stance (PR-01): a Switch Company = ALL caller keeps their own rows.
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteProductHandler(db, viewAll)
                .Handle(new DeleteProductCommand(foreignId), CancellationToken.None));

        var stored = await db.Products.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);

        await new DeleteProductHandler(db, user)
            .Handle(new DeleteProductCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(id), CancellationToken.None));
    }

    // The "delete only when unused" guard deferred by PD-01 / PD-03 and landed with HA-01:
    // a product a batch still holds cannot be freed (no spec message - ours).
    [Fact]
    public async Task Handle_ProductUsedByABatch_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        await SeedBatchProductLinkAsync(db, id);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(id), CancellationToken.None));

        Assert.Equal("This product is used by a batch.", exception.Message);
        Assert.False((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_ProductWithAnIngredientLink_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(db, id, rawMaterialId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(id), CancellationToken.None));

        Assert.Equal("This product is used by an ingredient link.", exception.Message);
        Assert.False((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_ProductWithAnImage_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        await SeedProductImageAsync(db, id, ProductImagePosition.Front);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new DeleteProductHandler(db, user)
                .Handle(new DeleteProductCommand(id), CancellationToken.None));

        Assert.Equal("This product is used by a product image.", exception.Message);
        Assert.False((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    // CodingRules 7.2: only LIVE referencing rows block - an unlinked (soft-deleted) batch
    // row does not hold the product back.
    [Fact]
    public async Task Handle_SoftDeletedBatchLink_DoesNotBlockTheDelete()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        await SeedBatchProductLinkAsync(db, id, softDeleteLink: true);

        // Same detach as the other delete tests: the handler tracks only its principal.
        db.ChangeTracker.Clear();

        await new DeleteProductHandler(db, user)
            .Handle(new DeleteProductCommand(id), CancellationToken.None);

        Assert.True((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }
}
