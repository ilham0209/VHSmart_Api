using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class GetMenuIngredientOptionsTests
{
    [Fact]
    public async Task Handle_OfferedMaterial_ComesBackWithTheSpecColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var materialId = await SeedRawMaterialAsync(db, "Oryza Noodles");
        var menuId = await SeedMenuAsync(db, rawMaterialIds: Array.Empty<Guid>());

        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal(materialId, row.Id);
        Assert.Equal("Oryza Noodles", row.Ingredient);
        Assert.Equal("Oryza sativa", row.ScientificName);
        Assert.Equal("Santan Foods", row.ManufacturerInformation.Name);
        Assert.Equal("Jalan Gombak 1", row.ManufacturerInformation.Address);
        Assert.Equal("0312345678", row.ManufacturerInformation.Contact);
        Assert.Null(row.HalalInformation);
    }

    [Fact]
    public async Task Handle_MaterialAlreadyOnTheMenu_IsNotOfferedAgain()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var linkedId = await SeedRawMaterialAsync(db, "Linked Ingredient");
        var otherId = await SeedRawMaterialAsync(db, "Free Ingredient");
        var menuId = await SeedMenuAsync(db, rawMaterialIds: new[] { linkedId });

        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None);

        Assert.Equal(otherId, Assert.Single(response.Data).Id);
    }

    [Fact]
    public async Task Handle_DroppedLinkRow_OffersTheMaterialAgain()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var materialId = await SeedRawMaterialAsync(db);
        var menuId = await SeedMenuAsync(db, rawMaterialIds: new[] { materialId });

        // Unlink it (the soft delete of an update)...
        var link = await db.MenuRawMaterials.SingleAsync(row => row.MenuId == menuId);
        db.MenuRawMaterials.Remove(link);
        await db.SaveChangesAsync();

        // ...so the picker offers it back: the link filter only counts live rows.
        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None);

        Assert.Equal(materialId, Assert.Single(response.Data).Id);
    }

    [Fact]
    public async Task Handle_UnknownMenu_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuIngredientOptionsHandler(db).Handle(
                new GetMenuIngredientOptionsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MenuOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var menuId = await SeedMenuAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuIngredientOptionsHandler(db).Handle(
                new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignUnsharedMaterial_IsNotOffered()
    {
        // CodingRules 7.3: the picker keeps the visibility filter - a material of another
        // company that was never shared with the caller can never be attached.
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRawMaterialAsync(db, "Foreign Ingredient", companyId: CompanyB);
        var menuId = await SeedMenuAsync(db);

        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_MaterialSharedWithTheCaller_IsOffered()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var sharedId = await SeedRawMaterialAsync(
            db, "Shared Ingredient", companyId: CompanyB, accessibleCompanyIds: new[] { CompanyA });
        var menuId = await SeedMenuAsync(db);

        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId), CancellationToken.None);

        Assert.Equal(sharedId, Assert.Single(response.Data).Id);
    }

    [Fact]
    public async Task Handle_SearchOnIngredient_FiltersTheRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRawMaterialAsync(db, "Rice Flour");
        await SeedRawMaterialAsync(db, "Cane Sugar");
        var menuId = await SeedMenuAsync(db, rawMaterialIds: Array.Empty<Guid>());

        var response = await new GetMenuIngredientOptionsHandler(db).Handle(
            new GetMenuIngredientOptionsQuery(menuId)
            {
                Request = new DataGridRequest { SearchTerm = "cane" }
            },
            CancellationToken.None);

        Assert.Equal("Cane Sugar", Assert.Single(response.Data).Ingredient);
    }
}
