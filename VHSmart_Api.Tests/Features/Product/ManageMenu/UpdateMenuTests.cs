using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class UpdateMenuTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedMenuAsync(db);
        var command = await UpdateCommandAsync(db, id, description: "Updated description");

        var response = await new UpdateMenuHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Nasi Lemak Updated", response.Name);
        Assert.Equal("Updated description", response.Description);
        Assert.NotNull(response.ModifiedDate);

        var stored = await db.Menus.AsNoTracking().SingleAsync(row => row.Id == id);
        Assert.Equal("Nasi Lemak Updated", stored.Name);
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(MenuStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Handle_DroppedRows_AreSoftDeletedAndNewRowsInserted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var oldCompany = await SeedCompanyAsync(db, "Old Partner");
        var keptCompany = await SeedCompanyAsync(db, "Kept Partner");
        var newCompany = await SeedCompanyAsync(db, "New Partner");
        var oldMaterial = await SeedRawMaterialAsync(db, "Old Ingredient");
        var keptMaterial = await SeedRawMaterialAsync(db, "Kept Ingredient");
        var newMaterial = await SeedRawMaterialAsync(db, "New Ingredient");
        var id = await SeedMenuAsync(
            db,
            accessibleCompanyIds: new[] { oldCompany, keptCompany },
            rawMaterialIds: new[] { oldMaterial, keptMaterial });

        var command = await UpdateCommandAsync(
            db,
            id,
            accessibleCompanyIds: new[] { keptCompany, newCompany },
            rawMaterialIds: new[] { keptMaterial, newMaterial });

        await new UpdateMenuHandler(db, user).Handle(command, CancellationToken.None);

        var companies = await db.MenuAccessibleCompanies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.MenuId == id)
            .ToListAsync();
        Assert.Equal(2, companies.Count(row => !row.IsDeleted));
        var droppedCompany = Assert.Single(companies, row => row.IsDeleted);
        Assert.Equal(oldCompany, droppedCompany.AccessibleCompanyId);

        var materials = await db.MenuRawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.MenuId == id)
            .ToListAsync();
        Assert.Equal(2, materials.Count(row => !row.IsDeleted));
        var droppedMaterial = Assert.Single(materials, row => row.IsDeleted);
        Assert.Equal(oldMaterial, droppedMaterial.RawMaterialId);

        // The kept row was left alone rather than deleted and re-created.
        Assert.Equal(
            1,
            await db.MenuRawMaterials.IgnoreQueryFilters().CountAsync(
                row => row.MenuId == id
                    && row.RawMaterialId == keptMaterial
                    && !row.IsDeleted));
    }

    [Fact]
    public async Task Handle_ReLinkingADroppedMaterial_CreatesOneLiveRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var materialId = await SeedRawMaterialAsync(db);
        var id = await SeedMenuAsync(db, rawMaterialIds: new[] { materialId });

        // Drop it...
        await new UpdateMenuHandler(db, user).Handle(
            await UpdateCommandAsync(db, id, rawMaterialIds: new[] { await SeedRawMaterialAsync(db, "Other") }),
            CancellationToken.None);
        // ...and link it back on the next save.
        await new UpdateMenuHandler(db, user).Handle(
            await UpdateCommandAsync(db, id, rawMaterialIds: new[] { materialId }),
            CancellationToken.None);

        var live = await db.MenuRawMaterials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.MenuId == id && row.RawMaterialId == materialId)
            .ToListAsync();

        Assert.Equal(2, live.Count);
        Assert.Equal(1, live.Count(row => !row.IsDeleted));
        Assert.Equal(1, live.Count(row => row.IsDeleted));
    }

    [Fact]
    public async Task Handle_DuplicateName_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedMenuCategoryAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak", categoryId: categoryId);
        var id = await SeedMenuAsync(db, name: "Mee Goreng", categoryId: categoryId);

        var command = await UpdateCommandAsync(db, id, name: "Nasi Lemak", categoryId: categoryId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateMenuHandler(db, user).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_KeepingItsOwnName_IsNotAConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedMenuAsync(db);

        var command = await UpdateCommandAsync(db, id, name: "Nasi Lemak");

        var response = await new UpdateMenuHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Nasi Lemak", response.Name);
    }

    [Fact]
    public async Task Handle_UnknownMenu_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMenuHandler(db, user).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MenuSharedWithTheCaller_ThrowsNotFound()
    {
        // Sharing is read-only (spec 9.2 / CodingRules 7.3): the reader sees the menu but
        // cannot save it - the answer is 404, never 403.
        var reader = UserB();
        var db = await CreateDbAsync(reader);
        await SeedCompanyAsync(db, "Beta Foods", id: CompanyB);
        var id = await SeedMenuAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: new[] { CompanyB });

        var command = await UpdateCommandAsync(db, id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMenuHandler(db, reader).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(command with { Name = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu name is required.");
    }

    [Fact]
    public async Task Validator_UnknownCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid(), categoryId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu category not found.");
    }

    [Fact]
    public async Task Validator_MissingListOfCompanies_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(
            command with { AccessibleCompanyIds = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "List of Company is required.");
    }

    [Fact]
    public async Task Validator_MissingListOfRawMaterials_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(
            command with { RawMaterialIds = Array.Empty<Guid>() });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "List of Raw Materials is required.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateMenuValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
