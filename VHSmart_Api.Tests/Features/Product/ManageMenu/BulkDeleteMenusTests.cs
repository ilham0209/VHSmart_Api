using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class BulkDeleteMenusTests
{
    [Fact]
    public async Task Handle_SeveralIds_SoftDeletesEveryRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var first = await SeedMenuAsync(db, name: "Nasi Lemak");
        var second = await SeedMenuAsync(db, name: "Mee Goreng");

        await new BulkDeleteMenusHandler(db, user).Handle(
            new BulkDeleteMenusCommand(new[] { first, second }), CancellationToken.None);

        Assert.Equal(
            2,
            await db.Menus.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
        Assert.Equal(
            0,
            await db.Menus.CountAsync());
    }

    [Fact]
    public async Task Handle_DuplicateIds_AreCollapsedToOneRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedMenuAsync(db);

        await new BulkDeleteMenusHandler(db, user).Handle(
            new BulkDeleteMenusCommand(new[] { id, id }), CancellationToken.None);

        Assert.Equal(
            1,
            await db.Menus.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundAndDeletesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedMenuAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteMenusHandler(db, user).Handle(
                new BulkDeleteMenusCommand(new[] { id, Guid.NewGuid() }),
                CancellationToken.None));

        // All-or-nothing: the first id was valid but nothing moved.
        Assert.False(
            (await db.Menus.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == id))
            .IsDeleted);
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        var id = await SeedMenuAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: new[] { CompanyB });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new BulkDeleteMenusHandler(db, UserB()).Handle(
                new BulkDeleteMenusCommand(new[] { id }), CancellationToken.None));

        Assert.False(
            (await db.Menus.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == id))
            .IsDeleted);
    }

    [Fact]
    public async Task Validator_NullIds_FailsWithNoRowsSelected()
    {
        var validator = new BulkDeleteMenusValidator();

        var result = await validator.ValidateAsync(
            new BulkDeleteMenusCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
    }

    [Fact]
    public async Task Validator_EmptyIds_FailsWithNoRowsSelected()
    {
        var validator = new BulkDeleteMenusValidator();

        var result = await validator.ValidateAsync(
            new BulkDeleteMenusCommand(Array.Empty<Guid>()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "No rows selected.");
    }

    [Fact]
    public async Task Validator_SeveralIds_Passes()
    {
        var validator = new BulkDeleteMenusValidator();

        var result = await validator.ValidateAsync(
            new BulkDeleteMenusCommand(new[] { Guid.NewGuid(), Guid.NewGuid() }));

        Assert.True(result.IsValid);
    }
}
