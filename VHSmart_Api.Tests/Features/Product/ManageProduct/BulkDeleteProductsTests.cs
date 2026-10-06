using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class BulkDeleteProductsTests
{
    [Fact]
    public async Task Handle_MultipleRows_SoftDeletesThemAll()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var first = await SeedProductAsync(db, code: "PRD-001");
        var second = await SeedProductAsync(db, name: "Santan Sos", code: "PRD-002");

        await new BulkDeleteProductsHandler(db, user)
            .Handle(new BulkDeleteProductsCommand([first, second]), CancellationToken.None);

        Assert.Empty(await db.Products.ToArrayAsync());
        Assert.Equal(2, await db.Products.IgnoreQueryFilters().CountAsync());
        Assert.All(
            await db.Products.IgnoreQueryFilters().ToListAsync(),
            row => Assert.True(row.IsDeleted));
    }

    [Fact]
    public async Task Handle_DuplicateIds_AreDeletedOnce()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);

        await new BulkDeleteProductsHandler(db, user)
            .Handle(new BulkDeleteProductsCommand([id, id]), CancellationToken.None);

        Assert.Equal(1, await db.Products.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundAndDeletesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteProductsHandler(db, user)
                .Handle(
                    new BulkDeleteProductsCommand([id, Guid.NewGuid()]),
                    CancellationToken.None));

        // All-or-nothing: a stale selection never half-applies.
        Assert.False((await db.Products.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndDeletesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var own = await SeedProductAsync(db, code: "PRD-001");
        var foreign = await SeedProductAsync(
            db, name: "Foreign", code: "PRD-002", companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteProductsHandler(db, user)
                .Handle(
                    new BulkDeleteProductsCommand([own, foreign]),
                    CancellationToken.None));

        Assert.Equal(
            0,
            await db.Products.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task Validator_MissingIds_Fails()
    {
        var validator = new BulkDeleteProductsValidator();

        var missing = await validator.ValidateAsync(new BulkDeleteProductsCommand(null));
        var empty = await validator.ValidateAsync(new BulkDeleteProductsCommand([]));

        Assert.False(missing.IsValid);
        Assert.Contains(
            missing.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
        Assert.False(empty.IsValid);
        Assert.Contains(
            empty.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
    }
}
