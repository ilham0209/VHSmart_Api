using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class GetMenuConceptByIdTests
{
    [Fact]
    public async Task Handle_Existing_ReturnsTheFormFieldsAndTheMenuList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, "Alpha Foods", id: CompanyA);
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedLinkAsync(db, conceptId, menuId);

        var response = await new GetMenuConceptByIdHandler(db)
            .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None);

        Assert.Equal(conceptId, response.Id);
        Assert.Equal("Breakfast Menu", response.Name);
        Assert.Equal("Rotating morning set", response.Description);
        Assert.Equal(CompanyA, response.CompanyId);
        Assert.Equal("Alpha Foods", response.CompanyName);

        var menu = Assert.Single(response.Menus);
        Assert.Equal(menuId, menu.MenuId);
        Assert.Equal("Nasi Lemak", menu.MenuName);
        Assert.Equal("Permanent", menu.MenuCategory);
        Assert.Equal(MenuConceptMenuMappingStatus.Active, menu.MappingStatus);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_ConceptWithNoMenus_ReturnsAnEmptyList()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);

        var response = await new GetMenuConceptByIdHandler(db)
            .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None);

        Assert.Empty(response.Menus);
    }

    [Fact]
    public async Task Handle_PairThatWasUnlinked_IsReturnedWithItsStatus()
    {
        // The unlinked pair keeps its row as INACTIVE (the PD-02 stance) so the table can show
        // it and a later Save can flip it back.
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Mee Goreng");
        await SeedLinkAsync(
            db, conceptId, menuId, mappingStatus: MenuConceptMenuMappingStatus.Inactive);

        var response = await new GetMenuConceptByIdHandler(db)
            .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None);

        Assert.Equal(
            MenuConceptMenuMappingStatus.Inactive,
            Assert.Single(response.Menus).MappingStatus);
    }

    [Fact]
    public async Task Handle_MenuDeletedAfterItWasLinked_DropsOutOfTheList()
    {
        // PD-04's DeleteMenu leaves the link rows behind on purpose - the join is what makes
        // the orphan invisible instead of rendering a blank row.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedLinkAsync(db, conceptId, menuId);
        db.Menus.Remove(await db.Menus.SingleAsync());
        await db.SaveChangesAsync();

        var response = await new GetMenuConceptByIdHandler(db)
            .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None);

        Assert.Empty(response.Menus);
    }

    [Fact]
    public async Task Handle_UnknownConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptByIdHandler(db)
                .Handle(new GetMenuConceptByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptByIdHandler(db)
                .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        db.MenuConcepts.Remove(await db.MenuConcepts.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptByIdHandler(db)
                .Handle(new GetMenuConceptByIdQuery(conceptId), CancellationToken.None));
    }
}
