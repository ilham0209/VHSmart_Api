using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditTask.AuditTaskTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

public class StartAuditAuditTaskTests
{
    private static StartAuditAuditTaskHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_StartsAudit_AndSnapshotsChecklistInNumberingOrder()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var member = Guid.Parse(user.UserId);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(db, member);

        await Handler(db, user).Handle(
            new StartAuditAuditTaskCommand(taskId), CancellationToken.None);

        var task = await db.AuditTasks.SingleAsync(row => row.Id == taskId);
        Assert.Equal(AuditTaskStatus.InProgress, task.Status);
        Assert.NotNull(task.StartAuditDate);
        Assert.NotNull(task.TimeIn);

        // Snapshot = the three criteria rows, frozen in checklist numbering order.
        var items = await db.AuditTaskItems.AsNoTracking()
            .Where(row => row.AuditTaskId == taskId)
            .OrderBy(row => row.CategorySequence)
            .ThenBy(row => row.CriteriaSequence)
            .ToListAsync();

        Assert.Equal(3, items.Count);

        Assert.Equal("PEST CONTROL", items[0].Category);
        Assert.Equal(1, items[0].CategorySequence);
        Assert.Equal("Clean floor", items[0].Criteria);
        Assert.Equal(1, items[0].CriteriaSequence);
        Assert.Equal("Detailed records kept", items[0].SubCriteria);
        Assert.Equal("ISO 4.5", items[0].Reference);
        Assert.Equal(2m, items[0].PotentialPoint);
        Assert.Null(items[0].ActualPoint);
        Assert.Null(items[0].IsConforming);

        Assert.Equal("PEST CONTROL", items[1].Category);
        Assert.Equal("No pest activity", items[1].Criteria);
        Assert.Equal(2, items[1].CriteriaSequence);
        Assert.Equal(3m, items[1].PotentialPoint);
        Assert.Null(items[1].SubCriteria);

        Assert.Equal("CLEANING", items[2].Category);
        Assert.Equal(2, items[2].CategorySequence);
        Assert.Equal("Clean floor", items[2].Criteria);
        Assert.Equal(1, items[2].CriteriaSequence);
        Assert.Equal(1m, items[2].PotentialPoint);
    }

    [Fact]
    public async Task Handle_SecondStart_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.InProgress);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new StartAuditAuditTaskCommand(taskId), CancellationToken.None));

        Assert.Equal("The audit has already been started.", exception.Message);
    }

    [Fact]
    public async Task Handle_ForeignTask_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new StartAuditAuditTaskCommand(taskId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonMember_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        // The group's member is someone else - the current user is no member (404, not 403).
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(db, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new StartAuditAuditTaskCommand(taskId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownTask_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new StartAuditAuditTaskCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedCriteria_AreExcludedFromTheSnapshot()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var member = Guid.Parse(user.UserId);
        var (taskId, _, _, checklistId, _) = await SeedStandardAuditAsync(db, member);

        // Soft-delete one linked criteria row (house pattern db.X.Remove()).
        var link = await db.AuditChecklistCriteria
            .Where(row => row.ChecklistId == checklistId)
            .FirstAsync();
        db.AuditChecklistCriteria.Remove(link);
        await db.SaveChangesAsync();

        await Handler(db, user).Handle(
            new StartAuditAuditTaskCommand(taskId), CancellationToken.None);

        Assert.Equal(2, await db.AuditTaskItems.CountAsync(row => row.AuditTaskId == taskId));
    }
}
