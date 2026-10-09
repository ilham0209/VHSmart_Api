using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class UpdateAuditChecklistTests
{
    private static UpdateAuditChecklistHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StoresHeaderAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db, name: "Syariah");
        var rowId = await SeedChecklistAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "checklist syariah 2.0", "Updated", null),
            CancellationToken.None);

        var stored = await db.AuditChecklists.SingleAsync();
        Assert.Equal(category, stored.ChecklistCategoryId);
        Assert.Equal("checklist syariah 2.0", stored.Name);
        Assert.Equal("Updated", stored.Description);
        Assert.Equal(CompanyA, stored.CompanyId);

        Assert.Equal(rowId, response.Id);
        Assert.Empty(response.CriteriaIds);
    }

    [Fact]
    public async Task Handle_LinkRebuild_TickUntickInOneSave()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var keepCriteria = await SeedCriteriaAsync(db, CompanyA, criteriaText: "Keep me");
        var dropCriteria = await SeedCriteriaAsync(db, CompanyA, criteriaText: "Drop me");
        var addCriteria = await SeedCriteriaAsync(db, CompanyA, criteriaText: "Add me");
        var rowId = await SeedChecklistAsync(db, CompanyA, categoryId: category);
        await SeedChecklistLinkAsync(db, CompanyA, rowId, keepCriteria);
        await SeedChecklistLinkAsync(db, CompanyA, rowId, dropCriteria);

        var response = await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "Checklist", null, [keepCriteria, addCriteria]),
            CancellationToken.None);

        // Step 2 of the two-step flow: the ticks of the Criteria Selection arrive with the
        // header save and are rebuilt - un-ticked rows soft-deleted, new ticks inserted.
        var links = await db.AuditChecklistCriteria.ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(rowId, link.ChecklistId));
        Assert.DoesNotContain(links, link => link.AuditCriteriaId == dropCriteria);
        Assert.Contains(links, link => link.AuditCriteriaId == addCriteria);

        Assert.Equal(2, response.CriteriaIds.Count);
        Assert.Contains(keepCriteria, response.CriteriaIds);
        Assert.Contains(addCriteria, response.CriteriaIds);
    }

    [Fact]
    public async Task Handle_ReTickAfterUntick_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var criteria = await SeedCriteriaAsync(db, CompanyA);
        var rowId = await SeedChecklistAsync(db, CompanyA, categoryId: category);
        await SeedChecklistLinkAsync(db, CompanyA, rowId, criteria);

        await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "Checklist", null, []),
            CancellationToken.None);
        var response = await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "Checklist", null, [criteria]),
            CancellationToken.None);

        // The junction's filtered UQ pair (Database.md 12) frees the pair on un-tick, so
        // the re-tick inserts a fresh live row.
        Assert.Contains(criteria, response.CriteriaIds);
        var live = await db.AuditChecklistCriteria.IgnoreQueryFilters()
            .Where(link => !link.IsDeleted && link.AuditCriteriaId == criteria)
            .CountAsync();
        Assert.Equal(1, live);
    }

    [Fact]
    public async Task Handle_UnknownRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditChecklistCommand(
                    Guid.NewGuid(), category, "Checklist", null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCategory = await SeedChecklistCategoryAsync(db, CompanyB);
        var foreignRow = await SeedChecklistAsync(
            db, CompanyB, categoryId: foreignCategory);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditChecklistCommand(
                    foreignRow, foreignCategory, "Checklist", null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ChecklistUsedByAPlan_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var rowId = await SeedChecklistAsync(db, CompanyA, categoryId: category);
        await SeedPlanAsync(db, CompanyA, rowId);

        // Spec 14.6 [MANUAL] + Database.md 12: any live AudAuditPlans row freezes the
        // checklist - 409, before anything is written.
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditChecklistCommand(
                    rowId, category, "Changed", null, null),
                CancellationToken.None));
        Assert.Contains("cannot be edited", exception.Message);

        Assert.Equal("Checklist testing 18 Nov 25",
            (await db.AuditChecklists.SingleAsync()).Name);
    }

    [Fact]
    public async Task Handle_PlanOfAnotherCompany_DoesNotLock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var rowId = await SeedChecklistAsync(db, CompanyA, categoryId: category);
        await SeedPlanAsync(db, CompanyB, rowId);

        // The lock query carries the caller's company - a foreign plan row (which could
        // only exist through bad data) must not freeze the own checklist.
        var response = await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "Changed", null, null),
            CancellationToken.None);

        Assert.Equal("Changed", response.Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedPlan_DoesNotLock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var rowId = await SeedChecklistAsync(db, CompanyA, categoryId: category);
        var planId = await SeedPlanAsync(db, CompanyA, rowId);
        db.AuditPlans.Remove(await db.AuditPlans.SingleAsync(plan => plan.Id == planId));
        await db.SaveChangesAsync();

        // The lock reads LIVE plans only (the tenant/soft-delete filter) - a cancelled
        // plan frees the checklist (spec 14.6 says "linked to", Database.md "used by any
        // row"; the house soft-delete default resolves it - flagged).
        var response = await Handler(db, user).Handle(
            new UpdateAuditChecklistCommand(
                rowId, category, "Changed", null, null),
            CancellationToken.None);

        Assert.Equal("Changed", response.Name);
    }

    [Fact]
    public async Task Validator_UnknownId_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new UpdateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditChecklistCommand(
                Guid.Empty, category, "Checklist", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "'Id' must not be empty.");
    }

    [Fact]
    public async Task Validator_UnknownCriteria_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var validator = new UpdateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditChecklistCommand(
                Guid.NewGuid(), category, "Checklist", null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Criteria not found.");
    }

    [Fact]
    public async Task Validator_CriteriaOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var foreignCriteria = await SeedCriteriaAsync(db, CompanyB);
        var validator = new UpdateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditChecklistCommand(
                Guid.NewGuid(), category, "Checklist", null, [foreignCriteria]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Criteria not found.");
    }

    [Fact]
    public async Task Validator_ValidShape_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var category = await SeedChecklistCategoryAsync(db);
        var criteria = await SeedCriteriaAsync(db, CompanyA);
        var validator = new UpdateAuditChecklistValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateAuditChecklistCommand(
                Guid.NewGuid(), category, "Checklist testing 18 Nov 25", "Description",
                [criteria]));

        Assert.True(result.IsValid);
    }
}
