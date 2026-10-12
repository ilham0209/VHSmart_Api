using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditTask.AuditTaskTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

public class UpdateAuditTaskFinalPointTests
{
    private static UpdateAuditTaskFinalPointHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_SetsFinalPoint_AndRecomputesFinalReviewScore()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.Completed, seedItems: false);
        // Potential 2 + 3 + 1 = 6; final 1 + 3 + 1 = 5 -> 83.33.
        var first = await SeedTaskItemAsync(
            db, CompanyA, taskId, 1, 1, 2m, actualPoint: 2m, finalPoint: 1m);
        await SeedTaskItemAsync(db, CompanyA, taskId, 1, 2, 3m, actualPoint: 3m, finalPoint: 3m);
        await SeedTaskItemAsync(db, CompanyA, taskId, 2, 1, 1m, actualPoint: 1m, finalPoint: 1m);

        await Handler(db, user).Handle(
            new UpdateAuditTaskFinalPointCommand(taskId, first, 1m), CancellationToken.None);

        var item = await db.AuditTaskItems.SingleAsync(row => row.Id == first);
        Assert.Equal(1m, item.FinalPoint);
        var task = await db.AuditTasks.SingleAsync(row => row.Id == taskId);
        Assert.Equal(83.33m, task.FinalReviewScore);
    }

    [Fact]
    public async Task Handle_FinalPointsWithoutValueCountAsZero()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.Completed, seedItems: false);
        // Only the first row gets a Final Point: 1 / 6 * 100 = 16.67.
        var first = await SeedTaskItemAsync(
            db, CompanyA, taskId, 1, 1, 2m, actualPoint: 2m);
        await SeedTaskItemAsync(db, CompanyA, taskId, 1, 2, 3m, actualPoint: 3m);
        await SeedTaskItemAsync(db, CompanyA, taskId, 2, 1, 1m, actualPoint: 1m);

        await Handler(db, user).Handle(
            new UpdateAuditTaskFinalPointCommand(taskId, first, 1m), CancellationToken.None);

        var task = await db.AuditTasks.SingleAsync(row => row.Id == taskId);
        Assert.Equal(16.67m, task.FinalReviewScore);
    }

    [Fact]
    public async Task Handle_BeforeCompletion_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.InProgress);
        var itemId = await SeedTaskItemAsync(db, CompanyA, taskId, 1, 1, 2m);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditTaskFinalPointCommand(taskId, itemId, 1m),
                CancellationToken.None));

        Assert.Equal(
            "Final Review is only available after the audit is completed.",
            exception.Message);
    }

    [Fact]
    public async Task Handle_UnknownItem_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.Completed, seedItems: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditTaskFinalPointCommand(taskId, Guid.NewGuid(), 1m),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingFinalPoint_Fails()
    {
        var results = await new UpdateAuditTaskFinalPointValidator().ValidateAsync(
            new UpdateAuditTaskFinalPointCommand(Guid.NewGuid(), Guid.NewGuid(), null));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Final Point is required.");
    }
}
