using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditCriteria.AuditCriteriaTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class UpdateAuditCriteriaTests
{
    private static UpdateAuditCriteriaHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StoresChangesAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db, name: "Storage");
        var criteria = await SeedMasterAsync(db, CompanyA, text: "Temperature control");
        var rowId = await SeedCriteriaAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new UpdateAuditCriteriaCommand(
                rowId, category, 3, criteria, 4, null,
                "ISO 22000", "Clause 8", 7m, "Updated description", null),
            CancellationToken.None);

        var stored = await db.AuditCriteria.SingleAsync();
        Assert.Equal(category, stored.CategoryId);
        Assert.Equal(3, stored.CategorySequence);
        Assert.Equal(criteria, stored.CriteriaId);
        Assert.Equal(4, stored.CriteriaSequence);
        Assert.Equal(7m, stored.PotentialPoint);
        Assert.Equal("Updated description", stored.Description);
        Assert.Equal(CompanyA, stored.CompanyId);

        Assert.Equal(rowId, response.Id);
        Assert.Equal(7m, response.PotentialPoint);
    }

    [Fact]
    public async Task Handle_EmptySubCriteriaId_ClearsTheLink()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var subCriteria = await SeedMasterAsync(
            db, CompanyA, AuditCriteriaMasterKind.SubCriteria, "Storage temperature");
        var rowId = await SeedCriteriaAsync(db, CompanyA, subCriteriaId: subCriteria);

        var response = await Handler(db, user).Handle(
            new UpdateAuditCriteriaCommand(
                rowId, category, 0, criteria, 0, Guid.Empty, null, null, null, null, null),
            CancellationToken.None);

        Assert.Null(response.SubCriteriaId);
        Assert.Null((await db.AuditCriteria.SingleAsync()).SubCriteriaId);
    }

    [Fact]
    public async Task Handle_LinkRebuild_RemovesUnlinkedAndAddsNewRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var keepFinding = await SeedFindingAsync(db, CompanyA, findingCode: "F111");
        var dropFinding = await SeedFindingAsync(db, CompanyA, findingCode: "F222");
        var addFinding = await SeedFindingAsync(db, CompanyA, findingCode: "F333");
        var rowId = await SeedCriteriaAsync(db, CompanyA);
        await SeedCriteriaFindingLinkAsync(db, CompanyA, rowId, keepFinding);
        await SeedCriteriaFindingLinkAsync(db, CompanyA, rowId, dropFinding);

        var response = await Handler(db, user).Handle(
            new UpdateAuditCriteriaCommand(
                rowId, category, 0, criteria, 0, null, null, null, null, null,
                [keepFinding, addFinding]),
            CancellationToken.None);

        var links = await db.AuditCriteriaFindings.ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(rowId, link.AuditCriteriaId));
        Assert.DoesNotContain(links, link => link.FindingId == dropFinding);
        Assert.Contains(links, link => link.FindingId == addFinding);

        Assert.Equal(2, response.Findings.Count);
        Assert.Contains(keepFinding, response.Findings);
        Assert.Contains(addFinding, response.Findings);
    }

    [Fact]
    public async Task Handle_UnlinkAll_ClearsEveryLink()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var finding = await SeedFindingAsync(db, CompanyA);
        var rowId = await SeedCriteriaAsync(db, CompanyA);
        await SeedCriteriaFindingLinkAsync(db, CompanyA, rowId, finding);

        var response = await Handler(db, user).Handle(
            new UpdateAuditCriteriaCommand(
                rowId, category, 0, criteria, 0, null, null, null, null, null, []),
            CancellationToken.None);

        Assert.Empty(response.Findings);
        Assert.Equal(0, await db.AuditCriteriaFindings.IgnoreQueryFilters().CountAsync(link => !link.IsDeleted));
    }

    [Fact]
    public async Task Handle_UnknownRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);

        // Unknown or foreign row -> 404 (CodingRules 9), never 403.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditCriteriaCommand(
                    Guid.NewGuid(), category, 0, criteria, 0, null, null, null, null, null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedCategoryAsync(db, CompanyB);
        var foreignCriteria = await SeedMasterAsync(db, CompanyB);
        var foreignRow = await SeedCriteriaAsync(
            db, CompanyB, categoryId: foreignCategory, criteriaId: foreignCriteria);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditCriteriaCommand(
                    foreignRow, foreignCategory, 0, foreignCriteria, 0,
                    null, null, null, null, null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_UnknownId_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditCriteriaCommand(
                Guid.Empty, Guid.NewGuid(), 0, Guid.NewGuid(), 0,
                null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "'Id' must not be empty.");
    }

    [Fact]
    public async Task Validator_ForeignCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedCategoryAsync(db, CompanyB);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var validator = new UpdateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditCriteriaCommand(
                Guid.NewGuid(), foreignCategory, 0, criteria, 0,
                null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_ForeignFinding_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var foreignFinding = await SeedFindingAsync(db, CompanyB);
        var validator = new UpdateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditCriteriaCommand(
                Guid.NewGuid(), category, 0, criteria, 0,
                null, null, null, null, null, [foreignFinding]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding not found.");
    }

    [Fact]
    public async Task Validator_ValidShape_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedCategoryAsync(db);
        var criteria = await SeedMasterAsync(db, CompanyA);
        var finding = await SeedFindingAsync(db, CompanyA);
        var validator = new UpdateAuditCriteriaValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditCriteriaCommand(
                Guid.NewGuid(), category, 1, criteria, 2, null,
                "ISO 22000", "Clause 7", 3m, "Description", [finding]));

        Assert.True(result.IsValid);
    }
}
