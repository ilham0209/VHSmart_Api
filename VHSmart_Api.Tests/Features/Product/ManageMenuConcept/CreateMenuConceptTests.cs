using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;
using static VHSmart_Api.Tests.Features.Product.ManageMenuConcept.MenuConceptTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class CreateMenuConceptTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresRowFromTheJwtCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, "Alpha Foods", id: CompanyA);

        var response = await new CreateMenuConceptHandler(db, user)
            .Handle(Command(), CancellationToken.None);

        Assert.Equal("Breakfast Menu", response.Name);
        Assert.Equal("Rotating morning set", response.Description);
        Assert.Equal(CompanyA, response.CompanyId);
        Assert.Equal("Alpha Foods", response.CompanyName);
        Assert.Empty(response.Menus);

        var stored = await db.MenuConcepts.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
    }

    [Fact]
    public async Task Handle_ValidCommand_WritesNoMenuLinks()
    {
        // spec 9.3: the modal saves the concept FIRST, the "List of Menu" is saved by the PUT.
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await new CreateMenuConceptHandler(db, user)
            .Handle(Command(), CancellationToken.None);

        Assert.Empty(response.Menus);
        Assert.Equal(0, await db.MenuConceptMenus.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_SameNameTwice_IsAllowed()
    {
        // Database.md 9 defines no unique index for PrdMenuConcepts and the legacy
        // "concept-name check" states no scope - the owner is asked instead of a rule invented.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var handler = new CreateMenuConceptHandler(db, user);

        await handler.Handle(Command(), CancellationToken.None);
        await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(2, await db.MenuConcepts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var validator = new CreateMenuConceptValidator();

        var result = await validator.ValidateAsync(Command() with { Name = null });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "Name"
                && failure.ErrorMessage == "Menu concept name is required.");
    }

    [Fact]
    public async Task Validator_NameOver200Characters_Fails()
    {
        var validator = new CreateMenuConceptValidator();

        var result = await validator.ValidateAsync(
            Command() with { Name = new string('a', 201) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu concept name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_DescriptionOver1000Characters_Fails()
    {
        var validator = new CreateMenuConceptValidator();

        var result = await validator.ValidateAsync(
            Command() with { Description = new string('a', 1001) });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_WhitespaceName_Fails()
    {
        var validator = new CreateMenuConceptValidator();

        var result = await validator.ValidateAsync(Command() with { Name = "   " });

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Menu concept name is required.");
    }
}
