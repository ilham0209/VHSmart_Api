using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
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
}
