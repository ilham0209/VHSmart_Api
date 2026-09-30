using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class CreateGeneralDataTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static CreateGeneralDataHandler Handler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StampsCurrentCompanyAndReturnsRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new CreateGeneralDataCommand(
                GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", "Own brand"),
            CancellationToken.None);

        Assert.Equal(GeneralDataGroup.COMPANY, response.Group);
        Assert.Equal("Brand", response.Category);
        Assert.Equal("Sahih Mart", response.Name);

        var stored = await db.GeneralData.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT only, never from the request (CodingRules 8.1, spec 3.3).
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("Own brand", stored.Description);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_DuplicateNameInSameGroupAndCategory_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await Handler(db, user).Handle(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCategoryOrGroup_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await Handler(db, user).Handle(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", null),
            CancellationToken.None);

        // Same name, different category; and same name in another group.
        var otherCategory = await Handler(db, user).Handle(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Ownership Type", "Sahih Mart", null),
            CancellationToken.None);
        var otherGroup = await Handler(db, user).Handle(
            new CreateGeneralDataCommand(GeneralDataGroup.PEOPLE, "Designation", "Sahih Mart", null),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, otherCategory.Id);
        Assert.NotEqual(Guid.Empty, otherGroup.Id);
        Assert.Equal(3, await db.GeneralData.CountAsync());
    }

    [Fact]
    public async Task Handle_MissingGroup_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, user).Handle(
                new CreateGeneralDataCommand(null, "Brand", "Sahih Mart", null),
                CancellationToken.None));
    }

    [Fact]
    public void Validator_RequiresGroupAndValidCategoryAndName()
    {
        var validator = new CreateGeneralDataValidator();

        var noGroup = validator.Validate(
            new CreateGeneralDataCommand(null, "Brand", "Sahih Mart", null));
        var foreignCategory = validator.Validate(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Audit Type", "Sahih Mart", null));
        var emptyName = validator.Validate(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", string.Empty, null));
        var valid = validator.Validate(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", null));

        Assert.False(noGroup.IsValid);
        Assert.Contains(noGroup.Errors, failure => failure.PropertyName == "Group");
        Assert.False(foreignCategory.IsValid);
        Assert.Contains(
            foreignCategory.Errors,
            failure => failure.ErrorMessage == "The category does not belong to the selected group.");
        Assert.False(emptyName.IsValid);
        Assert.Contains(emptyName.Errors, failure => failure.PropertyName == "Name");
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void Validator_CategoryMatchingIsCaseInsensitive()
    {
        var validator = new CreateGeneralDataValidator();

        var result = validator.Validate(
            new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "brand", "Sahih Mart", null));

        Assert.True(result.IsValid);
    }
}
