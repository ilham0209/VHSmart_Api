using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class UpdateMenuConceptTests
{
    [Fact]
    public async Task Handle_ValidCommand_SavesNameAndDescription()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var conceptId = await SeedConceptAsync(db);

        var response = await new UpdateMenuConceptHandler(db).Handle(
            UpdateCommand(conceptId,
                name: "Brunch Menu",
                description: "Late morning set",
                menuIds: Array.Empty<Guid>()),
            CancellationToken.None);

        Assert.Equal("Brunch Menu", response.Name);
        Assert.Equal("Late morning set", response.Description);

        var stored = await db.MenuConcepts.AsNoTracking().SingleAsync();
        Assert.Equal("Brunch Menu", stored.Name);
        Assert.Equal("Late morning set", stored.Description);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_NewMenuIds_CreateActiveLinkRows()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");

        await new UpdateMenuConceptHandler(db).Handle(
            UpdateCommand(conceptId, menuIds: new[] { menuId }),
            CancellationToken.None);

        var row = Assert.Single(await db.MenuConceptMenus.AsNoTracking().ToListAsync());
        Assert.Equal(menuId, row.MenuId);
        Assert.Equal(CompanyA, row.CompanyId);
        Assert.Equal(MenuConceptMenuMappingStatus.Active, row.MappingStatus);
    }

    [Fact]
    public async Task Handle_DroppedMenu_TurnsItsRowInactiveAndKeepsIt()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var keptMenuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        var droppedMenuId = await SeedMenuAsync(db, name: "Mee Goreng");
        await SeedLinkAsync(db, conceptId, keptMenuId);
        await SeedLinkAsync(db, conceptId, droppedMenuId);

        await new UpdateMenuConceptHandler(db).Handle(
            UpdateCommand(conceptId, menuIds: new[] { keptMenuId }),
            CancellationToken.None);

        var rows = await db.MenuConceptMenus.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(row => row.MenuId)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(
            MenuConceptMenuMappingStatus.Active,
            rows.Single(row => row.MenuId == keptMenuId).MappingStatus);
        Assert.Equal(
            MenuConceptMenuMappingStatus.Inactive,
            rows.Single(row => row.MenuId == droppedMenuId).MappingStatus);
    }

    [Fact]
    public async Task Handle_ReLinkingAnInactivePair_FlipsItBackToActiveWithoutASecondRow()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Mee Goreng");
        await SeedLinkAsync(
            db, conceptId, menuId, mappingStatus: MenuConceptMenuMappingStatus.Inactive);

        await new UpdateMenuConceptHandler(db).Handle(
            UpdateCommand(conceptId, menuIds: new[] { menuId }),
            CancellationToken.None);

        var row = Assert.Single(await db.MenuConceptMenus.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Equal(MenuConceptMenuMappingStatus.Active, row.MappingStatus);
    }

    [Fact]
    public async Task Handle_EmptyMenuIds_UnlinksEveryPair()
    {
        // The payload may be empty (a concept needs no menu in spec 9.3) - that is an
        // explicit unlink of everything, not "leave the list alone".
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedLinkAsync(db, conceptId, menuId);

        await new UpdateMenuConceptHandler(db).Handle(
            UpdateCommand(conceptId, menuIds: Array.Empty<Guid>()),
            CancellationToken.None);

        Assert.Equal(
            MenuConceptMenuMappingStatus.Inactive,
            (await db.MenuConceptMenus.IgnoreQueryFilters().AsNoTracking().SingleAsync())
            .MappingStatus);
    }

    [Fact]
    public async Task Handle_UnknownConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMenuConceptHandler(db).Handle(
                UpdateCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMenuConceptHandler(db).Handle(
                UpdateCommand(conceptId), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingMenuIds_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), menuIds: null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "MenuIds"
                && failure.ErrorMessage == "List of Menu is required.");
    }

    [Fact]
    public async Task Validator_EmptyMenuIds_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), menuIds: Array.Empty<Guid>()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UnknownMenuId_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), menuIds: new[] { Guid.NewGuid() }));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu not found.");
    }

    [Fact]
    public async Task Validator_MenuOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var foreignMenuId = await SeedMenuAsync(db, companyId: CompanyB, name: "Private Menu");
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), menuIds: new[] { foreignMenuId }));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu not found.");
    }

    [Fact]
    public async Task Validator_DuplicateMenuIdInPayload_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var menuId = await SeedMenuAsync(db);
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), menuIds: new[] { menuId, menuId }));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuConceptValidator(db);

        var result = await validator.ValidateAsync(
            UpdateCommand(Guid.NewGuid(), name: null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Name"
                && failure.ErrorMessage == "Menu concept name is required.");
    }
}
