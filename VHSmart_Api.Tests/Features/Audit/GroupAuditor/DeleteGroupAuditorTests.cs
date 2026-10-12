using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class DeleteGroupAuditorTests
{
    private static DeleteGroupAuditorHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesGroupAndMembers()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var second = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader, second]);

        await Handler(db, user).Handle(new DeleteGroupAuditorCommand(groupId), CancellationToken.None);

        var group = await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == groupId);
        Assert.True(group.IsDeleted);
        Assert.Equal(2, await db.GroupAuditorMembers.IgnoreQueryFilters()
            .CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task Handle_GroupWithoutMembers_DeletesTheGroup()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, []);

        await Handler(db, user).Handle(new DeleteGroupAuditorCommand(groupId), CancellationToken.None);

        var group = await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == groupId);
        Assert.True(group.IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handler(db, user).Handle(new DeleteGroupAuditorCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        var foreignId = await SeedGroupAsync(db, CompanyB, checklist, leader, [leader]);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handler(db, user).Handle(new DeleteGroupAuditorCommand(foreignId), CancellationToken.None));

        // IgnoreQueryFilters: the tenant filter hides another company's row from this context.
        Assert.False((await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == foreignId)).IsDeleted);
    }

    [Fact]
    public async Task Handle_SoftDeletedGroup_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader]);
        await Handler(db, user).Handle(new DeleteGroupAuditorCommand(groupId), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handler(db, user).Handle(new DeleteGroupAuditorCommand(groupId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GroupReferencedByPlan_StillDeletes()
    {
        // No lock: spec 14.6 freezes checklists once used by a plan but says nothing about
        // groups (flagged). A plan's GroupAuditorId soft-deleted group persists as a row, so
        // the Restrict FK is never violated.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader]);
        db.AuditPlans.Add(new AuditPlanEntity
        {
            CompanyId = CompanyA,
            AuditReferenceNo = $"REF-{Guid.NewGuid():N}",
            AuditPurposeId = Guid.NewGuid(),
            AuditTypeId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            PremiseId = Guid.NewGuid(),
            ScheduleDate = new DateTime(2026, 11, 18),
            GroupAuditorId = groupId,
            ChecklistId = checklist,
            AssignedByUserId = Guid.NewGuid(),
            DateAssigned = new DateTime(2026, 11, 1)
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Handler(db, user).Handle(new DeleteGroupAuditorCommand(groupId), CancellationToken.None);

        var plan = await db.AuditPlans.AsNoTracking().SingleAsync();
        Assert.Equal(groupId, plan.GroupAuditorId);
        Assert.False(plan.IsDeleted);
        Assert.True((await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == groupId)).IsDeleted);
    }
}
