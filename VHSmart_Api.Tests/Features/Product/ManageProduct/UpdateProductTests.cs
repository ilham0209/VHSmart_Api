using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class UpdateProductTests
{
    [Fact]
    public async Task Handle_ExistingRow_UpdatesEveryField()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        var marketingMethodId = await SeedMarketingMethodAsync(db);

        var response = await new UpdateProductHandler(db, user)
            .Handle(
                await UpdateCommandAsync(db, id, marketingMethodId: marketingMethodId),
                CancellationToken.None);

        Assert.Equal("Santan Kicap Updated", response.Name);
        Assert.Equal(marketingMethodId, response.MarketingMethodId);

        var stored = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Santan Kicap Updated", stored.Name);
        Assert.Equal(marketingMethodId, stored.MarketingMethodId);
        Assert.Equal(user.UserId, stored.SysUserModified);
    }

    [Fact]
    public async Task Handle_OptionalFieldsCleared_AreStoredAsNull()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedProductAsync(db);
        var marketingMethodId = await SeedMarketingMethodAsync(db);
        var handler = new UpdateProductHandler(db, user);
        await handler.Handle(
            await UpdateCommandAsync(db, id, marketingMethodId: marketingMethodId),
            CancellationToken.None);

        // A second edit that omits the optional dropdown clears it again.
        await handler.Handle(
            await UpdateCommandAsync(db, id, marketingMethodId: null),
            CancellationToken.None);

        var stored = await db.Products.AsNoTracking().SingleAsync();
        Assert.Null(stored.MarketingMethodId);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateProductHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndChangesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedProductAsync(db, name: "Foreign", companyId: CompanyB);
        var command = await UpdateCommandAsync(db, foreignId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateProductHandler(db, user)
                .Handle(command, CancellationToken.None));

        var stored = await db.Products.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Foreign", stored.Name);
    }

    [Fact]
    public async Task Handle_ViewAllToken_CannotEditAnotherCompanysRow()
    {
        // The premise stance (PR-01): writes stay inside the JWT company even for a
        // Switch Company = ALL caller (raw material's EnsureOwner allows it - flagged).
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        var foreignId = await SeedProductAsync(db, name: "Foreign", companyId: CompanyB);
        var command = await UpdateCommandAsync(db, foreignId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateProductHandler(db, viewAll)
                .Handle(command, CancellationToken.None));

        var stored = await db.Products.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Foreign", stored.Name);
    }

    [Fact]
    public async Task Validator_MissingId_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateProductValidator(db, UserA());

        var result = await validator.ValidateAsync(
            await UpdateCommandAsync(db, Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Id");
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateProductValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(command with { Name = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task Validator_UnknownBrand_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateProductValidator(db, UserA());
        var command = await UpdateCommandAsync(db, Guid.NewGuid());

        var result = await validator.ValidateAsync(command with { BrandId = Guid.NewGuid() });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new UpdateProductValidator(db, UserA());

        var result = await validator.ValidateAsync(
            await UpdateCommandAsync(db, Guid.NewGuid()));

        Assert.True(result.IsValid);
    }
}
