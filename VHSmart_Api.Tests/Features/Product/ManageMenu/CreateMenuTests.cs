using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class CreateMenuTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresRowFromTheJwtCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31));

        var response = await new CreateMenuHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Nasi Lemak", response.Name);
        Assert.Equal("Permanent", response.Category);
        Assert.Equal(new DateTime(2026, 1, 1), response.StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), response.EndDate);
        // The modal has no Status field: a menu is ACTIVE from creation (spec 9.2).
        Assert.Equal(MenuStatus.Active, response.Status);

        var stored = await db.Menus.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.Equal(MenuStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Handle_ValidCommand_WritesBothChildLists()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var partnerId = await SeedCompanyAsync(db, "Sharing Partner");
        var materialId = await SeedRawMaterialAsync(db, "Cocomilk");
        var command = await CommandAsync(
            db,
            accessibleCompanyIds: new[] { partnerId },
            rawMaterialIds: new[] { materialId });

        var response = await new CreateMenuHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(partnerId, Assert.Single(response.AccessibleFor).CompanyId);
        Assert.Equal("Sharing Partner", Assert.Single(response.AccessibleFor).CompanyName);
        Assert.Equal(materialId, Assert.Single(response.RawMaterials).Id);
        Assert.Equal("Cocomilk", Assert.Single(response.RawMaterials).Ingredient);

        Assert.Equal(partnerId, (await db.MenuAccessibleCompanies.AsNoTracking().SingleAsync()).AccessibleCompanyId);
        var link = await db.MenuRawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, link.CompanyId);
        Assert.Equal(materialId, link.RawMaterialId);
    }

    [Fact]
    public async Task Handle_DuplicateNameInSameCategory_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedMenuCategoryAsync(db);
        var handler = new CreateMenuHandler(db, user);

        await handler.Handle(
            await CommandAsync(db, categoryId: categoryId), CancellationToken.None);

        var duplicate = await CommandAsync(db, categoryId: categoryId);
        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCategory_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateMenuHandler(db, user);

        await handler.Handle(
            await CommandAsync(db, categoryId: await SeedMenuCategoryAsync(db, "Permanent")),
            CancellationToken.None);
        await handler.Handle(
            await CommandAsync(db, categoryId: await SeedMenuCategoryAsync(db, "Seasonal")),
            CancellationToken.None);

        Assert.Equal(2, await db.Menus.CountAsync());
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCompany_IsAllowed()
    {
        // UQ (CompanyId, CategoryId, Name): the same pair may exist in a second company.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedMenuCategoryAsync(db);
        await SeedMenuAsync(db, name: "Nasi Lemak", companyId: CompanyB, categoryId: categoryId);

        await new CreateMenuHandler(db, user)
            .Handle(await CommandAsync(db, categoryId: categoryId), CancellationToken.None);

        // The tenant filter hides company B's row, so the pair is counted across both companies.
        Assert.Equal(2, await db.Menus.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_FreesTheName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedMenuCategoryAsync(db);
        await SeedMenuAsync(db, categoryId: categoryId);
        var seeded = await db.Menus.SingleAsync();
        db.Menus.Remove(seeded);
        await db.SaveChangesAsync();

        await new CreateMenuHandler(db, user)
            .Handle(await CommandAsync(db, categoryId: categoryId), CancellationToken.None);

        // The unique index (and the handler check) only cover live rows: a deleted menu never
        // holds its name.
        Assert.Equal(1, await db.Menus.CountAsync(row => !row.IsDeleted));
        Assert.True(seeded.IsDeleted);
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Name = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Name"
                && failure.ErrorMessage == "Menu name is required.");
    }

    [Fact]
    public async Task Validator_NameOver200Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { Name = new string('a', 201) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_MissingCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { CategoryId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CategoryId"
                && failure.ErrorMessage == "Menu category is required.");
    }

    [Fact]
    public async Task Validator_UnknownCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db, categoryId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu category not found.");
    }

    [Fact]
    public async Task Validator_CategoryOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(
            db, categoryId: await SeedMenuCategoryAsync(db, companyId: CompanyB));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu category not found.");
    }

    [Fact]
    public async Task Validator_RowOfTheWrongCategoryAsMenuCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        // A Brand row is live COMPANY General Data but not the "Menu Category" dropdown.
        var command = await CommandAsync(db, categoryId: await SeedBrandAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu category not found.");
    }

    [Fact]
    public async Task Validator_DescriptionOver1000Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { Description = new string('a', 1001) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_MissingListOfWorkingCompanies_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { AccessibleCompanyIds = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "AccessibleCompanyIds"
                && failure.ErrorMessage == "List of Company is required.");
    }

    [Fact]
    public async Task Validator_EmptyListOfCompanies_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { AccessibleCompanyIds = Array.Empty<Guid>() });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "List of Company is required.");
    }

    [Fact]
    public async Task Validator_UnknownCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(
            db, accessibleCompanyIds: new[] { Guid.NewGuid() });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Company not found.");
    }

    [Fact]
    public async Task Validator_MissingListOfRawMaterials_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { RawMaterialIds = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "RawMaterialIds"
                && failure.ErrorMessage == "List of Raw Materials is required.");
    }

    [Fact]
    public async Task Validator_EmptyListOfRawMaterials_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { RawMaterialIds = Array.Empty<Guid>() });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "List of Raw Materials is required.");
    }

    [Fact]
    public async Task Validator_UnknownRawMaterial_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db, rawMaterialIds: new[] { Guid.NewGuid() });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Raw material not found.");
    }

    [Fact]
    public async Task Validator_RawMaterialNeverSharedWithTheCaller_Fails()
    {
        // CodingRules 7.3: the existence check runs through the visibility filter, so a
        // material of another company that was never shared cannot be attached.
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(
            db, rawMaterialIds: new[] { await SeedRawMaterialAsync(db, companyId: CompanyB) });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Raw material not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateMenuValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
