using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class GetAllMenuConceptsTests
{
    [Fact]
    public async Task Handle_OwnConcept_ReturnsTheSpecColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, "Alpha Foods", id: CompanyA);
        var conceptId = await SeedConceptAsync(db);
        var menuId = await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedLinkAsync(db, conceptId, menuId);

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("Breakfast Menu", row.Name);
        Assert.Equal("Rotating morning set", row.Description);
        Assert.Equal("Alpha Foods", row.CompanyName);
        Assert.Equal("Nasi Lemak", Assert.Single(row.Menus));
        Assert.Null(row.ModifiedDate);
    }

    [Fact]
    public async Task Handle_PairThatIsNotListed_IsNotInTheMenuColumn()
    {
        // The list column is the concept's menus: only ACTIVE pairs count, an unlinked pair
        // stays on the detail table with its own status instead.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var conceptId = await SeedConceptAsync(db);
        await SeedLinkAsync(db, conceptId, await SeedMenuAsync(db, name: "Nasi Lemak"));
        await SeedLinkAsync(
            db,
            conceptId,
            await SeedMenuAsync(db, name: "Mee Goreng"),
            mappingStatus: MenuConceptMenuMappingStatus.Inactive);

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Equal("Nasi Lemak", Assert.Single(Assert.Single(response.Data).Menus));
    }

    [Fact]
    public async Task Handle_NoRows_ReturnsAnEmptyPage()
    {
        var db = await CreateDbAsync(UserA());

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_SearchOnName_FiltersTheRows()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db, name: "Breakfast Menu");
        await SeedConceptAsync(db, name: "Dinner Menu");

        var response = await new GetAllMenuConceptsHandler(db).Handle(
            new GetAllMenuConceptsQuery
            {
                Request = new DataGridRequest { SearchTerm = "dinner" }
            },
            CancellationToken.None);

        Assert.Equal("Dinner Menu", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_SearchOnDescription_FiltersTheRows()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db, name: "Breakfast Menu", description: "Morning set");
        await SeedConceptAsync(db, name: "Dinner Menu", description: "Evening set");

        var response = await new GetAllMenuConceptsHandler(db).Handle(
            new GetAllMenuConceptsQuery
            {
                Request = new DataGridRequest { SearchTerm = "evening" }
            },
            CancellationToken.None);

        Assert.Equal("Dinner Menu", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsHidden()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db);
        db.MenuConcepts.Remove(await db.MenuConcepts.SingleAsync());
        await db.SaveChangesAsync();

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_IsNotReturned()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db, companyId: CompanyB);

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_ConceptOfAnotherCompany_IsSeenByASwitchCompanyAllToken()
    {
        // There is no "Accessible For" table for concepts: the plain tenant filter is the whole
        // visibility rule, so Switch Company = ALL (platform admin) sees every company's rows.
        var db = await CreateDbAsync(new TestCurrentUser(
            Guid.NewGuid().ToString(), CompanyA, isPlatformAdmin: true, viewAllCompanies: true));
        await SeedConceptAsync(db, companyId: CompanyB);

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Single(response.Data);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsTheConceptName()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db, name: "Dinner Menu");
        await SeedConceptAsync(db, name: "Breakfast Menu");

        var response = await new GetAllMenuConceptsHandler(db)
            .Handle(new GetAllMenuConceptsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Breakfast Menu", "Dinner Menu" },
            response.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_SortByDescriptionDescending_ObesysTheClient()
    {
        var db = await CreateDbAsync(UserA());
        await SeedConceptAsync(db, name: "Breakfast Menu", description: "Alpha set");
        await SeedConceptAsync(db, name: "Dinner Menu", description: "Zulu set");

        var response = await new GetAllMenuConceptsHandler(db).Handle(
            new GetAllMenuConceptsQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = nameof(GetAllMenuConceptsResponse.Description),
                    SortDescending = true
                }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Zulu set", "Alpha set" },
            response.Data.Select(row => row.Description).ToArray());
    }
}
