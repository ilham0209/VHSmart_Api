using VHSmart_Api.Features.Product.ManageMenu;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class GetMenuOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsTheOwnMenuCategoriesOrderedByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuCategoryAsync(db, name: "Seasonal");
        await SeedMenuCategoryAsync(db, name: "Permanent");

        var response = await new GetMenuOptionsHandler(db, user)
            .Handle(new GetMenuOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Permanent", "Seasonal" },
            response.Categories.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_ExcludesTheOtherCompaniesCategories()
    {
        // Spec 5.1: reference data is per company - another company's "Menu Category" row is
        // never offered as this menu's category.
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedMenuCategoryAsync(db, name: "Foreign Category", companyId: CompanyB);

        var response = await new GetMenuOptionsHandler(db, user)
            .Handle(new GetMenuOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Categories);
    }

    [Fact]
    public async Task Handle_ExcludesRowsOfAnotherDropdownOrGroup()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBrandAsync(db);
        await SeedProductCategoryAsync(db);
        await SeedMenuCategoryAsync(db);

        var response = await new GetMenuOptionsHandler(db, user)
            .Handle(new GetMenuOptionsQuery(), CancellationToken.None);

        Assert.Equal("Permanent", Assert.Single(response.Categories).Name);
    }

    [Fact]
    public async Task Handle_ReturnsTheCompanyListForThePicker()
    {
        // The "Accessible For" picker names OTHER companies, so the list is not tenant scoped -
        // the same stance the raw material options endpoint takes.
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        await SeedCompanyAsync(db, "Alpha Foods", id: CompanyA);

        var response = await new GetMenuOptionsHandler(db, user)
            .Handle(new GetMenuOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Foods", "Beta Foods" },
            response.Companies.Select(row => row.Name).ToArray());
    }
}
