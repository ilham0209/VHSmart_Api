using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class CreateProductTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresRowFromTheJwtCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var marketingMethodId = await SeedMarketingMethodAsync(db);
        var command = await CommandAsync(db, marketingMethodId: marketingMethodId);

        var response = await new CreateProductHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("Santan Kicap", response.Name);
        Assert.Equal("Santan Foods", response.ManufacturerName);
        Assert.Equal(command.BrandId, response.BrandId);
        Assert.Equal(command.CategoryId, response.CategoryId);
        Assert.Equal(marketingMethodId, response.MarketingMethodId);
        // The QR panel shows "QR Code yet to be generated" while QrCodeKey is null, and the
        // scheme-specific fields are not seen yet (9.1).
        Assert.Null(response.QrCodeKey);

        var stored = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.Equal("PRD-001", stored.Code);
        Assert.Null(stored.SchemeSpecificData);
        Assert.Null(stored.VerifyHalalPublishStatus);
    }

    [Fact]
    public async Task Handle_MarketingMethodOmitted_IsStoredAsNull()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await new CreateProductHandler(db, user)
            .Handle(await CommandAsync(db), CancellationToken.None);

        Assert.Null(response.MarketingMethodId);
        Assert.Null((await db.Products.AsNoTracking().SingleAsync()).MarketingMethodId);
    }

    [Fact]
    public async Task Handle_UnknownScheme_FailsValidation()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db, schemeId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Product scheme not found.");
    }

    [Fact]
    public async Task Validator_MissingScheme_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { SchemeId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "SchemeId"
                && failure.ErrorMessage == "Product scheme is required.");
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { Name = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Name"
                && failure.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task Validator_NameOver200Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { Name = new string('a', 201) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_MissingManufacturer_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { ManufacturerSupplierId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "ManufacturerSupplierId"
                && failure.ErrorMessage == "Manufacturer is required.");
    }

    [Fact]
    public async Task Validator_UnknownManufacturer_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db, manufacturerId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_SupplierOnlyManufacturer_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, manufacturerId: await SeedSupplierOnlyAsync(db));

        var result = await validator.ValidateAsync(command);

        // The options endpoint offers no supplier-only row, so a hand-crafted id must not
        // slip through either (9.1 "Manufacturer*").
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_ManufacturerOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, manufacturerId: await SeedManufacturerAsync(db, CompanyB));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Manufacturer not found.");
    }

    [Fact]
    public async Task Validator_MissingBrand_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { BrandId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "BrandId"
                && failure.ErrorMessage == "Brand is required.");
    }

    [Fact]
    public async Task Validator_RowOfTheWrongCategoryAsBrand_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        // A Product Category row is the wrong dropdown even though it is live General Data.
        var command = await CommandAsync(
            db, brandId: await SeedProductCategoryAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_BrandOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, brandId: await SeedBrandAsync(db, companyId: CompanyB));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_MissingCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(command with { CategoryId = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CategoryId"
                && failure.ErrorMessage == "Category is required.");
    }

    [Fact]
    public async Task Validator_RowOfTheWrongCategoryAsCategory_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, categoryId: await SeedBrandAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_UnknownMarketingMethod_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db, marketingMethodId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "MarketingMethodId"
                && failure.ErrorMessage == "Marketing method not found.");
    }

    [Fact]
    public async Task Validator_MarketingMethodOfAnotherCompany_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, marketingMethodId: await SeedMarketingMethodAsync(db, companyId: CompanyB));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Marketing method not found.");
    }

    [Fact]
    public async Task Validator_CodeOver500Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { Code = new string('a', 501) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Code must be 500 characters or fewer.");
    }

    [Fact]
    public async Task Validator_GtinOver50Characters_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(db);

        var result = await validator.ValidateAsync(
            command with { Gtin = new string('9', 51) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "GTIN must be 50 characters or fewer.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateProductValidator(db, UserA());
        var command = await CommandAsync(
            db, marketingMethodId: await SeedMarketingMethodAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_DuplicateProductName_IsAllowed()
    {
        // Database.md 9 defines no unique index and the legacy name check is [VERIFY]:
        // two live rows may carry the same name (flagged in the report).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateProductHandler(db, user);

        await handler.Handle(await CommandAsync(db), CancellationToken.None);
        await handler.Handle(
            await CommandAsync(db, code: "PRD-002"), CancellationToken.None);

        Assert.Equal(2, await db.Products.CountAsync());
    }
}
