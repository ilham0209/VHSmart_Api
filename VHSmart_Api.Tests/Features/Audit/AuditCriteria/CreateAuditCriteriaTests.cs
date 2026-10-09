using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class CreateAuditCriteriaTests
{
    private static CreateAuditCriteriaHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesRowAndLinksForTheJwtCompanyAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Storage temperature");
        var finding1 = await SeedFindingAsync(db, CompanyA, findingCode: "F111");
        var finding2 = await SeedFindingAsync(db, CompanyA, findingCode: "F222");

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaCommand(
                category, 2, criteria, 1, subCriteria,
                "ISO 22000", "Clause 7", 5m, "Kitchen cleanliness checks", [finding1, finding2]),
            CancellationToken.None);

        var stored = await db.AuditCriteria.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(category, stored.CategoryId);
        Assert.Equal(criteria, stored.CriteriaId);
        Assert.Equal(subCriteria, stored.SubCriteriaId);
        Assert.False(stored.IsDeleted);

        var links = await db.AuditCriteriaFindings.ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link =>
        {
            Assert.Equal(CompanyA, link.CompanyId);
            Assert.Equal(stored.Id, link.AuditCriteriaId);
        });

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal(5m, response.PotentialPoint);
        Assert.Equal(2, response.Findings.Count);
    }

    [Fact]
    public async Task Handle_NoPotentialPoint_DefaultsToOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaCommand(category, 0, criteria, 0, null, null, null, null, null, null),
            CancellationToken.None);

        // v4.9.2 shows 1 in the modal; the default is applied in the handler - no DDL
        // DEFAULT (the explicit-0 pitfall of AU-01).
        Assert.Equal(1m, response.PotentialPoint);
        Assert.Equal(1m, (await db.AuditCriteria.SingleAsync()).PotentialPoint);
    }

    [Fact]
    public async Task Handle_EmptySubCriteriaId_StoredAsNull()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaCommand(category, 0, criteria, 0, Guid.Empty, null, null, null, null, null),
            CancellationToken.None);

        Assert.Null(response.SubCriteriaId);
        Assert.Null((await db.AuditCriteria.SingleAsync()).SubCriteriaId);
    }

    [Fact]
    public async Task Handle_DuplicateFindingIds_CollapseToOneLinkRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var finding = await SeedFindingAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateAuditCriteriaCommand(category, 0, criteria, 0, null, null, null, null, null, [finding, finding]),
            CancellationToken.None);

        Assert.Single(response.Findings);
        Assert.Single(await db.AuditCriteriaFindings.ToListAsync());
    }

    [Fact]
    public async Task Validator_MissingCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(Guid.Empty, 0, criteria, 0, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category is required.");
    }

    [Fact]
    public async Task Validator_CategoryOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedCategoryAsync(db, CompanyB);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(foreignCategory, 0, criteria, 0, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_ExternalAuditCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var externalCategory = await SeedExternalCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        // The Category dropdown is the "Internal - Audit Category" list (spec 14.1
        // [VERIFY], question 36); the External pair of the same AUDIT group is refused.
        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(externalCategory, 0, criteria, 0, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_SubCriteriaKindOnCriteriaId_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Storage temperature");
        var validator = new CreateAuditCriteriaValidator(db, user);

        // Criteria* must be a Criteria kind row; a Sub Criteria value is refused there.
        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, subCriteria, 0, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Criteria not found.");
    }

    [Fact]
    public async Task Validator_CriteriaKindOnSubCriteriaId_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        // The reverse: a Criteria value offered for the Sub Criteria* dropdown is refused.
        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, criteria, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Sub Criteria not found.");
    }

    [Fact]
    public async Task Validator_UnknownFinding_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, null, null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding not found.");
    }

    [Fact]
    public async Task Validator_FindingOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var foreignFinding = await SeedFindingAsync(db, CompanyB);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, null, null, [foreignFinding]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding not found.");
    }

    [Fact]
    public async Task Validator_NegativePotentialPoint_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, -1m, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Potential Point must be 0 or more.");
    }

    [Fact]
    public async Task Validator_ReferenceCategoryLongerThan100_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, new string('x', 101), null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Reference Category must be 100 characters or fewer.");
    }

    [Fact]
    public async Task Validator_ReferenceLongerThan200_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, new string('x', 201), null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Reference must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan1000_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, null, new string('x', 1001), null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_LiveOwnCompanyValues_Pass()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Storage temperature");
        var finding = await SeedFindingAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 1, criteria, 2, subCriteria, "ISO 22000", "Clause 7",
                3m, "Kitchen cleanliness checks", [finding]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_ZeroPotentialPoint_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new CreateAuditCriteriaValidator(db, user);

        // The [VERIFY] of question 36 is whether 0 is allowed at all; until the owner
        // answers, the range check accepts 0 (flagged) - the modal default stays 1.
        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, 0m, null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_RepeatedCriteriaTextAcrossRows_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        await SeedCriteriaAsync(db, CompanyA, categoryId: category, criteriaId: criteria);
        var validator = new CreateAuditCriteriaValidator(db, user);

        // Spec 14.5's own sample repeats the same criteria text across rows, so the row
        // itself carries no unique rule (flagged vs the master UQ).
        var result = await validator.ValidateAsync(
            new CreateAuditCriteriaCommand(
                category, 0, criteria, 0, null, null, null, null, null, null));

        Assert.True(result.IsValid);
    }
}
