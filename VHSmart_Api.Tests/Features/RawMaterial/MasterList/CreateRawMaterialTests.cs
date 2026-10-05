using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class CreateRawMaterialTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresRowAndSharesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db);

        var response = await new CreateRawMaterialHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(RawMaterialCategory.Core, response.Category);
        Assert.Equal("Rice Flour", response.Ingredient);
        Assert.Equal("RM-001", response.IngredientCode);
        Assert.Equal("Santan Foods", response.ManufacturerName);
        Assert.Equal("Sharing Partner", Assert.Single(response.AccessibleFor).CompanyName);

        var stored = await db.RawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);

        var sharing = await db.RawMaterialAccessibleCompanies.AsNoTracking().SingleAsync();
        Assert.Equal(stored.Id, sharing.RawMaterialId);
        Assert.Equal(response.AccessibleFor.Single().CompanyId, sharing.AccessibleCompanyId);
    }

    [Fact]
    public async Task Handle_DuplicateIngredientCodeInSameCompany_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateRawMaterialHandler(db, user);
        await handler.Handle(await CommandAsync(db), CancellationToken.None);

        var duplicateCommand = await CommandAsync(db, ingredient: "Corn Flour");
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(duplicateCommand, CancellationToken.None));

        Assert.Equal("An ingredient with this code already exists.", exception.Message);
    }

    [Fact]
    public async Task Handle_DuplicateIngredientCodeInAnotherCompany_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await new CreateRawMaterialHandler(db, user)
            .Handle(await CommandAsync(db), CancellationToken.None);

        await SeedRowAsync(
            db,
            ingredient: "Corn Flour",
            ingredientCode: "RM-001",
            companyId: CompanyB,
            ingredientStatusId: await SeedGeneralDataAsync(db, "Ingredient Status", "Active", CompanyB),
            ingredientSourceId: await SeedGeneralDataAsync(db, "Ingredient Source", "Plant Based", CompanyB),
            manufacturerId: await SeedManufacturerAsync(db, CompanyB));

        // D-17 is per company: the same code may exist in both companies.
        Assert.Equal(2, await db.RawMaterials.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_BlankIngredientCode_IsStoredAsNullAndNeverConflicts()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateRawMaterialHandler(db, user);

        await handler.Handle(await CommandAsync(db, ingredientCode: "   "), CancellationToken.None);
        await handler.Handle(
            await CommandAsync(db, ingredient: "Corn Flour", ingredientCode: null),
            CancellationToken.None);

        var codes = await db.RawMaterials
            .AsNoTracking()
            .Select(row => row.IngredientCode)
            .ToListAsync();
        Assert.Equal(2, codes.Count);
        Assert.All(codes, code => Assert.Null(code));
    }

    [Fact]
    public async Task Handle_SoftDeletedRowWithSameCode_DoesNotConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateRawMaterialHandler(db, user);
        var first = await handler.Handle(await CommandAsync(db), CancellationToken.None);

        // Free-code check: soft-delete the row the way a delete request would (fresh scope, so
        // the sharing rows the create handler stored in this context are detached first).
        db.ChangeTracker.Clear();
        db.RawMaterials.Remove(await db.RawMaterials.SingleAsync());
        await db.SaveChangesAsync();

        var second = await handler.Handle(
            await CommandAsync(db, ingredient: "Corn Flour"), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.RawMaterials.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Category = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Category"
                && failure.ErrorMessage == "Category is required.");
    }

    [Fact]
    public async Task Validator_CategoryOutsideTheEnum_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Category = (RawMaterialCategory)99 });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Category"
                && failure.ErrorMessage == "Category is invalid.");
    }

    [Fact]
    public async Task Validator_MissingIngredientStatus_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { IngredientStatusId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "IngredientStatusId"
                && failure.ErrorMessage == "Ingredient status is required.");
    }

    [Fact]
    public async Task Validator_IngredientStatusOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var foreignStatus = await SeedGeneralDataAsync(db, "Ingredient Status", "Active", CompanyB);
        var command = await CommandAsync(db, ingredientStatusId: foreignStatus);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Ingredient status not found.");
    }

    [Fact]
    public async Task Validator_MissingIngredient_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Ingredient = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Ingredient"
                && failure.ErrorMessage == "Ingredient is required.");
    }

    [Fact]
    public async Task Validator_IngredientOver200Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Ingredient = new string('a', 201) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Ingredient");
    }

    [Fact]
    public async Task Validator_IngredientCodeOver50Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { IngredientCode = new string('a', 51) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "IngredientCode");
    }

    [Fact]
    public async Task Validator_MissingIngredientSource_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { IngredientSourceId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "IngredientSourceId"
                && failure.ErrorMessage == "Ingredient source is required.");
    }

    [Fact]
    public async Task Validator_IngredientSourceOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var foreignSource = await SeedGeneralDataAsync(db, "Ingredient Source", "Animal Based", CompanyB);
        var command = await CommandAsync(db, ingredientSourceId: foreignSource);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Ingredient source not found.");
    }

    [Fact]
    public async Task Validator_MissingManufacturer_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { ManufacturerSupplierId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "ManufacturerSupplierId"
                && failure.ErrorMessage == "Manufacturer is required.");
    }

    [Fact]
    public async Task Validator_ManufacturerOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var foreignManufacturer = await SeedManufacturerAsync(db, CompanyB);
        var command = await CommandAsync(db, manufacturerId: foreignManufacturer);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_MissingAccessibleFor_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var missing = await validator.ValidateAsync(command with { AccessibleCompanyIds = null });
        var empty = await validator.ValidateAsync(command with { AccessibleCompanyIds = [] });

        Assert.False(missing.IsValid);
        Assert.Contains(
            missing.Errors,
            failure => failure.ErrorMessage == "Accessible For is required.");
        Assert.False(empty.IsValid);
        Assert.Contains(
            empty.Errors,
            failure => failure.ErrorMessage == "Accessible For is required.");
    }

    [Fact]
    public async Task Validator_UnknownAccessibleCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { AccessibleCompanyIds = [Guid.NewGuid()] });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.ErrorMessage == "Company not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateRawMaterialValidator(db, UserA());

        var result = await validator.ValidateAsync(await CommandAsync(db));

        Assert.True(result.IsValid);
    }
}
