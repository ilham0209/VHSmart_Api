using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class DeleteMenuTests
{
    [Fact]
    public async Task Handle_OwnMenu_IsSoftDeletedAndKeepsItsChildRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var partnerId = await SeedCompanyAsync(db, "Sharing Partner");
        var materialId = await SeedRawMaterialAsync(db);
        var id = await SeedMenuAsync(
            db,
            accessibleCompanyIds: new[] { partnerId },
            rawMaterialIds: new[] { materialId });

        await new DeleteMenuHandler(db, user)
            .Handle(new DeleteMenuCommand(id), CancellationToken.None);

        var stored = await db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(row => row.Id == id);
        Assert.True(stored.IsDeleted);

        // CodingRules 7.1 / the child list stance: the link rows stay with the menu.
        Assert.Single(
            await db.MenuAccessibleCompanies
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(row => row.MenuId == id)
                .ToListAsync());
        Assert.Single(
            await db.MenuRawMaterials
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(row => row.MenuId == id)
                .ToListAsync());

        // The list no longer answers for it.
        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_UnknownMenu_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuHandler(db, UserA())
                .Handle(new DeleteMenuCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedMenu_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedMenuAsync(db);
        db.Menus.Remove(await db.Menus.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuHandler(db, user)
                .Handle(new DeleteMenuCommand(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MenuOfAnotherCompany_ThrowsNotFoundAndDeletesNothing()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedMenuAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuHandler(db, UserA())
                .Handle(new DeleteMenuCommand(id), CancellationToken.None));

        Assert.False(
            (await db.MenuRawMaterials
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(row => row.MenuId == id)
                .AnyAsync(row => row.IsDeleted)));
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_ThrowsNotFound()
    {
        // Sharing is read-only: the reader sees the menu in the list but the delete answers
        // 404 (never 403 - CodingRules 9), and the owner's row is untouched.
        var db = await CreateDbAsync(UserA());
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        var id = await SeedMenuAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: new[] { CompanyB });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMenuHandler(db, UserB())
                .Handle(new DeleteMenuCommand(id), CancellationToken.None));

        Assert.False(
            (await db.Menus
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(row => row.Id == id))
            .IsDeleted);
    }
}
