using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditTask.AuditTaskTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

public class UpdateAuditTaskItemTests
{
    private static UpdateAuditTaskItemHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static async Task<Guid> StartAndGetFirstItemAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid taskId)
    {
        await new StartAuditAuditTaskHandler(db, user).Handle(
            new StartAuditAuditTaskCommand(taskId), CancellationToken.None);
        return await db.AuditTaskItems
            .Where(row => row.AuditTaskId == taskId)
            .OrderBy(row => row.CategorySequence)
            .ThenBy(row => row.CriteriaSequence)
            .Select(row => row.Id)
            .FirstAsync();
    }

    [Fact]
    public async Task Handle_AnswersTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(db, Guid.Parse(user.UserId));
        var itemId = await StartAndGetFirstItemAsync(db, user, taskId);

        await Handler(db, user).Handle(
            new UpdateAuditTaskItemCommand(itemId, 2m, false, "Pest traces behind the chiller."),
            CancellationToken.None);

        var item = await db.AuditTaskItems.SingleAsync(row => row.Id == itemId);
        Assert.Equal(2m, item.ActualPoint);
        Assert.False(item.IsConforming);
        Assert.Equal("Pest traces behind the chiller.", item.Remarks);
    }

    [Fact]
    public async Task Handle_NotStartedTask_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(db, Guid.Parse(user.UserId));
        var itemId = await SeedTaskItemAsync(db, CompanyA, taskId, 1, 1, 2m);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditTaskItemCommand(itemId, 1m, true, null),
                CancellationToken.None));

        Assert.Equal("The audit has not been started yet.", exception.Message);
    }

    [Fact]
    public async Task Handle_CompletedTask_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.Completed);
        var itemId = await SeedTaskItemAsync(db, CompanyA, taskId, 1, 1, 2m, actualPoint: 2m);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditTaskItemCommand(itemId, 1m, true, null),
                CancellationToken.None));

        Assert.Equal(
            "The audit is completed and can no longer be edited.", exception.Message);
    }

    [Fact]
    public async Task Handle_ForeignItem_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.NewGuid(), AuditTaskStatus.InProgress);
        var itemId = await SeedTaskItemAsync(db, CompanyA, taskId, 1, 1, 2m);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateAuditTaskItemCommand(itemId, 1m, null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingActualPoint_Fails()
    {
        var results = await new UpdateAuditTaskItemValidator().ValidateAsync(
            new UpdateAuditTaskItemCommand(Guid.NewGuid(), null, null, null));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Actual Point is required.");
    }

    [Fact]
    public async Task Validator_NegativeActualPoint_Fails()
    {
        var results = await new UpdateAuditTaskItemValidator().ValidateAsync(
            new UpdateAuditTaskItemCommand(Guid.NewGuid(), -1m, null, null));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Actual Point cannot be negative.");
    }

    [Fact]
    public async Task Validator_TooLongRemarks_Fails()
    {
        var results = await new UpdateAuditTaskItemValidator().ValidateAsync(
            new UpdateAuditTaskItemCommand(Guid.NewGuid(), 1m, null, new string('x', 1001)));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors,
            error => error.ErrorMessage == "Remarks cannot exceed 1000 characters.");
    }
}
