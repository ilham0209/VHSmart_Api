using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class GetMenuByIdTests
{
    [Fact]
    public async Task Handle_ExistingMenu_ReturnsTheFormFieldsAndBothLists()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var partnerId = await SeedCompanyAsync(db, "Sharing Partner");
        var materialId = await SeedRawMaterialAsync(db, "Cocomilk");
        var id = await SeedMenuAsync(
            db,
            categoryId: await SeedMenuCategoryAsync(db),
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 12, 31),
            accessibleCompanyIds: new[] { partnerId },
            rawMaterialIds: new[] { materialId });

        var response = await new GetMenuByIdHandler(db)
            .Handle(new GetMenuByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal("Nasi Lemak", response.Name);
        Assert.Equal("Permanent", response.Category);
        Assert.Equal("Everyday rice set", response.Description);
        Assert.Equal(new DateTime(2026, 1, 1), response.StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), response.EndDate);
        Assert.Equal(MenuStatus.Active, response.Status);

        var company = Assert.Single(response.AccessibleFor);
        Assert.Equal(partnerId, company.CompanyId);
        Assert.Equal("Sharing Partner", company.CompanyName);

        var ingredient = Assert.Single(response.RawMaterials);
        Assert.Equal(materialId, ingredient.Id);
        Assert.Equal("Cocomilk", ingredient.Ingredient);
        Assert.Equal("Oryza sativa", ingredient.ScientificName);
        Assert.Equal("Santan Foods", ingredient.ManufacturerInformation.Name);
        Assert.Equal("Jalan Gombak 1", ingredient.ManufacturerInformation.Address);
        Assert.Equal("0312345678", ingredient.ManufacturerInformation.Contact);
        Assert.Null(ingredient.HalalInformation);
    }

    [Fact]
    public async Task Handle_UnknownMenu_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuByIdHandler(db)
                .Handle(new GetMenuByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MenuOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedMenuAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMenuByIdHandler(db)
                .Handle(new GetMenuByIdQuery(id), CancellationToken.None));
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
            new GetMenuByIdHandler(db)
                .Handle(new GetMenuByIdQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_IsReadable()
    {
        // CodingRules 7.3: a shared menu answers 200 for the company it was shared with, and
        // its child lists are read with it (the link rows belong to the owner's tenant scope).
        var reader = UserB();
        var db = await CreateDbAsync(reader);
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        var ownerCategory = await SeedMenuCategoryAsync(db, companyId: CompanyA);
        var materialId = await SeedRawMaterialAsync(db, companyId: CompanyA);
        var id = await SeedMenuAsync(
            db,
            companyId: CompanyA,
            categoryId: ownerCategory,
            accessibleCompanyIds: new[] { CompanyB },
            rawMaterialIds: new[] { materialId });

        var response = await new GetMenuByIdHandler(db)
            .Handle(new GetMenuByIdQuery(id), CancellationToken.None);

        Assert.Equal("Nasi Lemak", response.Name);
        Assert.Equal("Permanent", response.Category);
        Assert.Equal("Rice Flour", Assert.Single(response.RawMaterials).Ingredient);
        Assert.Equal("Beta Foods", Assert.Single(response.AccessibleFor).CompanyName);
        Assert.Equal(reader.CompanyId, Assert.Single(response.AccessibleFor).CompanyId);
    }
}
