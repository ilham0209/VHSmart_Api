using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class CreateAuditChecklistTests
{
    private static CreateAuditChecklistHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesHeaderForTheJwtCompanyWithoutCriteria()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);

        var response = await Handler(db, user).Handle(
            new CreateAuditChecklistCommand(
                category, "Checklist testing 18 Nov 25", "Do not use"),
            CancellationToken.None);

        var stored = await db.AuditChecklists.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(category, stored.ChecklistCategoryId);
        Assert.Equal("Checklist testing 18 Nov 25", stored.Name);
        Assert.False(stored.IsDeleted);

        // Two-step flow (spec 14.6 [MANUAL]): step 1 saves the header only - the Criteria
        // Selection arrives with the subsequent update.
        Assert.Empty(response.CriteriaIds);
        Assert.Equal(0, await db.AuditChecklistCriteria.CountAsync());
        Assert.Equal(stored.Id, response.Id);
    }

    [Fact]
    public async Task Handle_DuplicateName_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        await SeedChecklistAsync(db, CompanyA, categoryId: category, name: "Checklist syariah 2.0");

        // Database.md 12 states NO unique on Name - "checklist syariah 2.0" style rows are
        // ordinary duplicates.
        var response = await Handler(db, user).Handle(
            new CreateAuditChecklistCommand(category, "Checklist syariah 2.0", null),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.AuditChecklists.CountAsync());
    }

    [Fact]
    public async Task Validator_MissingCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(Guid.Empty, "Checklist", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Category is required.");
    }

    [Fact]
    public async Task Validator_UnknownCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(Guid.NewGuid(), "Checklist", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Category not found.");
    }

    [Fact]
    public async Task Validator_CategoryOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedChecklistCategoryAsync(db, CompanyB);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(foreignCategory, "Checklist", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Category not found.");
    }

    [Fact]
    public async Task Validator_ExternalAuditCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var externalCategory = await SeedExternalCategoryAsync(db);
        var validator = new CreateAuditChecklistValidator(db, user);

        // The dropdown is the "Internal - Audit Category" list (§14.1 [VERIFY], flagged);
        // the External pair of the same AUDIT group is refused.
        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(externalCategory, "Checklist", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Category not found.");
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(category, string.Empty, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Name is required.");
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(category, new string('x', 201), null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Checklist Name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan1000_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(category, "Checklist", new string('x', 1001)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_ValidShape_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new CreateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditChecklistCommand(
                category, "Checklist testing 18 Nov 25", "Description"));

        Assert.True(result.IsValid);
    }
}
