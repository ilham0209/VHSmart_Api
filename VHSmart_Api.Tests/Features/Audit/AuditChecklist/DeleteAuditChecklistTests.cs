using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditChecklist.AuditChecklistTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class DeleteAuditChecklistTests
{
    private static DeleteAuditChecklistHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedChecklistAsync(db, CompanyA);

        await Handler(db, user).Handle(
            new DeleteAuditChecklistCommand(rowId), CancellationToken.None);

        // Soft delete - the interceptor flips the state to Modified with IsDeleted = true
        // (the house pattern); no hard delete ever reaches the store.
        var stored = await db.AuditChecklists.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Equal(rowId, stored.Id);
        Assert.Equal(0, await db.AuditChecklists.CountAsync());
    }

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesItsCriteriaLinksFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var criteria = await SeedCriteriaAsync(db, CompanyA);
        var rowId = await SeedChecklistAsync(db, CompanyA);
        await SeedChecklistLinkAsync(db, CompanyA, rowId, criteria);

        // The link rows belong to the checklist: they go before the parent (the
        // Users/DeleteUser order - FK non-nullable + Restrict) or they orphan.
        await Handler(db, user).Handle(
            new DeleteAuditChecklistCommand(rowId), CancellationToken.None);

        var links = await db.AuditChecklistCriteria.IgnoreQueryFilters().ToListAsync();
        Assert.All(links, link => Assert.True(link.IsDeleted));
        Assert.True((await db.AuditChecklists.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new DeleteAuditChecklistCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignRow = await SeedChecklistAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new DeleteAuditChecklistCommand(foreignRow), CancellationToken.None));

        Assert.Equal(1, await db.AuditChecklists.IgnoreQueryFilters()
            .CountAsync(row => !row.IsDeleted));
    }

    [Fact]
    public async Task Handle_ChecklistUsedByAPlan_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedChecklistAsync(db, CompanyA);
        await SeedPlanAsync(db, CompanyA, rowId);

        // Spec 14.6 [MANUAL] + Database.md 12: the same computed lock on delete - 409
        // after the 404 lookup, nothing written.
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new DeleteAuditChecklistCommand(rowId), CancellationToken.None));
        Assert.Contains("cannot be deleted", exception.Message);

        Assert.Equal(1, await db.AuditChecklists.IgnoreQueryFilters()
            .CountAsync(row => !row.IsDeleted));
    }

    [Fact]
    public async Task Handle_PlanOfAnotherCompany_DoesNotLock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rowId = await SeedChecklistAsync(db, CompanyA);
        await SeedPlanAsync(db, CompanyB, rowId);

        await Handler(db, user).Handle(
            new DeleteAuditChecklistCommand(rowId), CancellationToken.None);

        Assert.Equal(0, await db.AuditChecklists.CountAsync());
    }
}
