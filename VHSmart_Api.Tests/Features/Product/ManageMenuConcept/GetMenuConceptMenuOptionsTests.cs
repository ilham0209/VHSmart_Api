using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class GetMenuConceptMenuOptionsTests
{
    [Fact]
    public async Task Handle_ExistingConcept_OffersOnlyMenusTheConceptDoesNotHaveYet()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        var activeMenuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        var inactiveMenuId = await SeedMenuAsync(db, name: "Mee Goreng");
        var freeMenuId = await SeedMenuAsync(db, name: "Ayam Penyet");
        await SeedLinkAsync(db, conceptId, activeMenuId);
        await SeedLinkAsync(
            db, conceptId, inactiveMenuId, mappingStatus: MenuConceptMenuMappingStatus.Inactive);

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        // Both live pairs are excluded, INACTIVE included: the unlinked pair stays on the
        // concept's own "List of Menu" with its Link icon (PD-02's rule), so offering it here
        // would create a second live row for the same pair (Database.md 9 UQ).
        var option = Assert.Single(response.Data);
        Assert.Equal(freeMenuId, option.Id);
        Assert.Equal("Ayam Penyet", option.Name);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_ConceptWithNoMenus_OffersEveryVisibleMenu()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        Assert.Equal(
            new[] { "Mee Goreng", "Nasi Lemak" },
            response.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_CategoryIsFilledForTheOption()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak", categoryId: await SeedMenuCategoryAsync(db));

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        Assert.Equal("Permanent", Assert.Single(response.Data).Category);
    }

    [Fact]
    public async Task Handle_SearchOnName_FiltersTheRows()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId)
            {
                Request = new DataGridRequest { SearchTerm = "mee" }
            },
            CancellationToken.None);

        Assert.Equal("Mee Goreng", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_MenuOfAnotherCompany_IsNotOffered()
    {
        // The "Link" picker runs under the "Accessible For" filter of CodingRules 7.3: a menu
        // another company never shared with the caller is not linkable, so it is not offered.
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        await SeedMenuAsync(db, companyId: CompanyB, name: "Private Menu");
        await SeedMenuAsync(db, name: "Nasi Lemak");

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        Assert.Equal("Nasi Lemak", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_IsOffered()
    {
        var db = await CreateDbAsync(UserB());
        var conceptId = await SeedConceptAsync(db, companyId: CompanyB);
        await SeedMenuAsync(
            db,
            companyId: CompanyA,
            name: "Nasi Lemak",
            accessibleCompanyIds: new[] { CompanyA, CompanyB });

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        Assert.Equal("Nasi Lemak", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_DeletedMenu_IsNotOffered()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");
        db.Menus.Remove(await db.Menus.SingleAsync(row => row.Name == "Nasi Lemak"));
        await db.SaveChangesAsync();

        var response = await new GetMenuConceptMenuOptionsHandler(db).Handle(
            new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None);

        Assert.Equal("Mee Goreng", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_UnknownConcept_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptMenuOptionsHandler(db).Handle(
                new GetMenuConceptMenuOptionsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptMenuOptionsHandler(db).Handle(
                new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ConceptDeletedAfterItWasSeeded_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var conceptId = await SeedConceptAsync(db);
        db.MenuConcepts.Remove(await db.MenuConcepts.SingleAsync());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuConceptMenuOptionsHandler(db).Handle(
                new GetMenuConceptMenuOptionsQuery(conceptId), CancellationToken.None));
    }
}
