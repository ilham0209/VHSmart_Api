using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class GetAllMenusTests
{
    [Fact]
    public async Task Handle_OwnMenu_ReturnsTheSpecColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var partnerId = await SeedCompanyAsync(db, "Sharing Partner");
        var materialId = await SeedRawMaterialAsync(db, "Cocomilk");
        await SeedMenuAsync(
            db,
            categoryId: await SeedMenuCategoryAsync(db),
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 12, 31),
            accessibleCompanyIds: new[] { partnerId },
            rawMaterialIds: new[] { materialId });

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("Nasi Lemak", row.Name);
        Assert.Equal("Everyday rice set", row.Description);
        Assert.Equal("Permanent", row.Category);
        Assert.Equal(new DateTime(2026, 1, 1), row.StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), row.EndDate);
        Assert.Equal(MenuStatus.Active, row.Status);
        Assert.Equal("Cocomilk", Assert.Single(row.Ingredients));
        Assert.Equal("Sharing Partner", Assert.Single(row.Companies));
        Assert.Null(row.ModifiedDate);
    }

    [Fact]
    public async Task Handle_NoRows_ReturnsAnEmptyPage()
    {
        var db = await CreateDbAsync(UserA());

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_SearchOnName_FiltersTheRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");

        var response = await new GetAllMenusHandler(db).Handle(
            new GetAllMenusQuery
            {
                Request = new DataGridRequest { SearchTerm = "mee" }
            },
            CancellationToken.None);

        Assert.Equal("Mee Goreng", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_SearchOnDescription_FiltersTheRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db, name: "Nasi Lemak", description: "Rice set");
        await SeedMenuAsync(db, name: "Mee Goreng", description: "Noodles");

        var response = await new GetAllMenusHandler(db).Handle(
            new GetAllMenusQuery
            {
                Request = new DataGridRequest { SearchTerm = "noodle" }
            },
            CancellationToken.None);

        Assert.Equal("Mee Goreng", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsHidden()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db);
        db.Menus.Remove(await db.Menus.SingleAsync());
        await db.SaveChangesAsync();

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_MenuOfAnotherCompany_IsNotReturned()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db, companyId: CompanyB);

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_IsReturnedWithItsLists()
    {
        // CodingRules 7.3: company B may READ a menu company A shared with it, and the menu's
        // category, ingredient list and company list travel with it - the child rows belong to
        // A's tenant scope and must not blank the row out.
        var userB = UserB();
        var db = await CreateDbAsync(userB);
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        var ownerCategory = await SeedMenuCategoryAsync(db, companyId: CompanyA);
        var materialId = await SeedRawMaterialAsync(db, companyId: CompanyA);
        await SeedMenuAsync(
            db,
            name: "Nasi Lemak",
            companyId: CompanyA,
            categoryId: ownerCategory,
            accessibleCompanyIds: new[] { CompanyB },
            rawMaterialIds: new[] { materialId });

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("Nasi Lemak", row.Name);
        Assert.Equal("Permanent", row.Category);
        Assert.Equal("Rice Flour", Assert.Single(row.Ingredients));
        Assert.Equal("Beta Foods", Assert.Single(row.Companies));
    }

    [Fact]
    public async Task Handle_SortByMenuDescending_ObesysTheClient()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");

        var response = await new GetAllMenusHandler(db).Handle(
            new GetAllMenusQuery
            {
                Request = new DataGridRequest { SortBy = "Name", SortDescending = true }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Nasi Lemak", "Mee Goreng" },
            response.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsTheMenuName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuAsync(db, name: "Nasi Lemak");
        await SeedMenuAsync(db, name: "Mee Goreng");

        var response = await new GetAllMenusHandler(db)
            .Handle(new GetAllMenusQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Mee Goreng", "Nasi Lemak" },
            response.Data.Select(row => row.Name).ToArray());
    }
}
