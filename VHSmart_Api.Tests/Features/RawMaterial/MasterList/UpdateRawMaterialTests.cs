using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class UpdateRawMaterialTests
{
    private static UpdateRawMaterialCommand ToUpdate(
        Guid id,
        CreateRawMaterialCommand create,
        IReadOnlyList<Guid>? accessibleCompanyIds = null) =>
        new(
            id,
            create.Category,
            create.IngredientStatusId,
            create.Ingredient,
            create.IngredientCode,
            create.CommercialName,
            create.ScientificName,
            create.IngredientSourceId,
            create.ManufacturerSupplierId,
            create.IsPackagingMaterial,
            accessibleCompanyIds ?? create.AccessibleCompanyIds);

    [Fact]
    public async Task Handle_ExistingRow_StoresChanges()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db, accessibleCompanyIds: [await SeedCompanyAsync(db, "Partner")]);
        var command = ToUpdate(id, await CommandAsync(db, ingredient: "Corn Flour"));

        var response = await new UpdateRawMaterialHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Corn Flour", response.Ingredient);

        var stored = await db.RawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal("Corn Flour", stored.Ingredient);
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserModified);
    }

    [Fact]
    public async Task Handle_DuplicateIngredientCodeOfAnotherRow_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedRowAsync(db, ingredientCode: "RM-001");
        var secondId = await SeedRowAsync(db, ingredient: "Corn Flour", ingredientCode: "RM-002");
        var command = ToUpdate(secondId, await CommandAsync(db, ingredient: "Corn Flour", ingredientCode: "RM-001"));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateRawMaterialHandler(db, user).Handle(command, CancellationToken.None));

        Assert.Equal("An ingredient with this code already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_KeepingItsOwnCode_DoesNotConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRowAsync(db, ingredientCode: "RM-001");
        var command = ToUpdate(id, await CommandAsync(db, ingredientCode: "RM-001"));

        var response = await new UpdateRawMaterialHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("RM-001", response.IngredientCode);
    }

    [Fact]
    public async Task Handle_ReconcilesTheAccessibleForList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var keptCompany = await SeedCompanyAsync(db, "Kept Partner");
        var removedCompany = await SeedCompanyAsync(db, "Removed Partner");
        var addedCompany = await SeedCompanyAsync(db, "Added Partner");
        var id = await SeedRowAsync(db, accessibleCompanyIds: [keptCompany, removedCompany]);

        var command = ToUpdate(
            id, await CommandAsync(db), accessibleCompanyIds: [keptCompany, addedCompany]);
        await new UpdateRawMaterialHandler(db, user).Handle(command, CancellationToken.None);

        var rows = await db.RawMaterialAccessibleCompanies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.RawMaterialId == id)
            .ToListAsync();

        // The kept row keeps its identity, the removed one is soft deleted, the added one is new.
        Assert.Equal(3, rows.Count);
        Assert.Single(rows, row => row.AccessibleCompanyId == keptCompany && !row.IsDeleted);
        Assert.Single(rows, row => row.AccessibleCompanyId == removedCompany && row.IsDeleted);
        Assert.Single(rows, row => row.AccessibleCompanyId == addedCompany && !row.IsDeleted);
    }

    [Fact]
    public async Task Handle_UnsharedRowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);
        var command = ToUpdate(foreignId, await CommandAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateRawMaterialHandler(db, user).Handle(command, CancellationToken.None));

        var stored = await db.RawMaterials.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Rice Flour", stored.Ingredient);
    }

    [Fact]
    public async Task Handle_RowSharedToTheCaller_IsReadOnly()
    {
        var userB = UserB();
        var db = await CreateDbAsync(userB);
        var sharedId = await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);
        var command = ToUpdate(sharedId, await CommandAsync(db));

        // The visibility filter shows the shared row (so the list and the detail work), but
        // only its owner may change it.
        Assert.NotNull(await db.RawMaterials.AsNoTracking().FirstOrDefaultAsync(
            row => row.Id == sharedId));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateRawMaterialHandler(db, userB).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ViewAllToken_MayEditAnotherCompanysRow()
    {
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        var foreignId = await SeedRowAsync(
            db, ingredient: "Shared Corn", companyId: CompanyB);
        var command = ToUpdate(foreignId, await CommandAsync(db, ingredient: "Shared Corn Updated"));

        var response = await new UpdateRawMaterialHandler(db, viewAll)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Shared Corn Updated", response.Ingredient);
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());
        var command = ToUpdate(Guid.NewGuid(), await CommandAsync(db));

        var missingCategory = await validator.ValidateAsync(command with { Category = null });
        var missingStatus = await validator.ValidateAsync(command with { IngredientStatusId = null });
        var missingIngredient = await validator.ValidateAsync(command with { Ingredient = null });
        var missingSource = await validator.ValidateAsync(command with { IngredientSourceId = null });
        var missingManufacturer = await validator.ValidateAsync(command with { ManufacturerSupplierId = null });
        var missingAccessible = await validator.ValidateAsync(command with { AccessibleCompanyIds = null });
        var emptyAccessible = await validator.ValidateAsync(command with { AccessibleCompanyIds = [] });

        Assert.False(missingCategory.IsValid);
        Assert.Contains(
            missingCategory.Errors,
            failure => failure.ErrorMessage == "Category is required.");
        Assert.False(missingStatus.IsValid);
        Assert.Contains(
            missingStatus.Errors,
            failure => failure.ErrorMessage == "Ingredient status is required.");
        Assert.False(missingIngredient.IsValid);
        Assert.Contains(
            missingIngredient.Errors,
            failure => failure.ErrorMessage == "Ingredient is required.");
        Assert.False(missingSource.IsValid);
        Assert.Contains(
            missingSource.Errors,
            failure => failure.ErrorMessage == "Ingredient source is required.");
        Assert.False(missingManufacturer.IsValid);
        Assert.Contains(
            missingManufacturer.Errors,
            failure => failure.ErrorMessage == "Manufacturer is required.");
        Assert.False(missingAccessible.IsValid);
        Assert.Contains(
            missingAccessible.Errors,
            failure => failure.ErrorMessage == "Accessible For is required.");
        Assert.False(emptyAccessible.IsValid);
        Assert.Contains(
            emptyAccessible.Errors,
            failure => failure.ErrorMessage == "Accessible For is required.");
    }

    [Fact]
    public async Task Validator_CategoryOutsideTheEnum_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());
        var command = ToUpdate(Guid.NewGuid(), await CommandAsync(db));

        var result = await validator.ValidateAsync(command with { Category = (RawMaterialCategory)99 });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category is invalid.");
    }

    [Fact]
    public async Task Validator_DropdownRowsOfAnotherCompany_Fail()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());
        var foreignStatus = await SeedGeneralDataAsync(db, "Ingredient Status", "Active", CompanyB);
        var foreignSource = await SeedGeneralDataAsync(db, "Ingredient Source", "Animal Based", CompanyB);
        var foreignManufacturer = await SeedManufacturerAsync(db, CompanyB);
        var command = ToUpdate(Guid.NewGuid(), await CommandAsync(db));

        var statusResult = await validator.ValidateAsync(
            command with { IngredientStatusId = foreignStatus });
        var sourceResult = await validator.ValidateAsync(
            command with { IngredientSourceId = foreignSource });
        var manufacturerResult = await validator.ValidateAsync(
            command with { ManufacturerSupplierId = foreignManufacturer });

        Assert.False(statusResult.IsValid);
        Assert.Contains(statusResult.Errors, failure => failure.ErrorMessage == "Ingredient status not found.");
        Assert.False(sourceResult.IsValid);
        Assert.Contains(sourceResult.Errors, failure => failure.ErrorMessage == "Ingredient source not found.");
        Assert.False(manufacturerResult.IsValid);
        Assert.Contains(manufacturerResult.Errors, failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_LengthLimits_Fail()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());
        var command = ToUpdate(Guid.NewGuid(), await CommandAsync(db));

        var longIngredient = await validator.ValidateAsync(
            command with { Ingredient = new string('a', 201) });
        var longCode = await validator.ValidateAsync(
            command with { IngredientCode = new string('a', 51) });

        Assert.False(longIngredient.IsValid);
        Assert.Contains(longIngredient.Errors, failure => failure.PropertyName == "Ingredient");
        Assert.False(longCode.IsValid);
        Assert.Contains(longCode.Errors, failure => failure.PropertyName == "IngredientCode");
    }

    [Fact]
    public async Task Validator_UnknownAccessibleCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());
        var command = ToUpdate(Guid.NewGuid(), await CommandAsync(db));

        var result = await validator.ValidateAsync(
            command with { AccessibleCompanyIds = [Guid.NewGuid()] });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.ErrorMessage == "Company not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateRawMaterialValidator(db, UserA());

        var result = await validator.ValidateAsync(
            ToUpdate(Guid.NewGuid(), await CommandAsync(db)));

        Assert.True(result.IsValid);
    }
}
