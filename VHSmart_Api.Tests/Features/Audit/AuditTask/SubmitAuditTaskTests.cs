using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditTask.AuditTaskTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

public class SubmitAuditTaskTests
{
    private static SubmitAuditTaskHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static async Task AnswerAllItemsAsync(
        TestableVHSmartDbContext db,
        Guid taskId,
        IReadOnlyList<decimal?> actualPoints)
    {
        var items = await db.AuditTaskItems
            .Where(row => row.AuditTaskId == taskId)
            .OrderBy(row => row.CategorySequence)
            .ThenBy(row => row.CriteriaSequence)
            .ToListAsync();
        for (var index = 0; index < items.Count; index++)
        {
            items[index].ActualPoint = actualPoints[index];
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_SubmitsTheAudit_AndAppliesD06Score()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.InProgress);
        // Potential 2 + 3 + 1 = 6; actual 2 + 3 + 0 = 5 -> 83.33 (D-06, 2 decimals).
        await AnswerAllItemsAsync(db, taskId, [2m, 3m, 0m]);

        await Handler(db, user).Handle(
            new SubmitAuditTaskCommand(
                taskId,
                "All criteria met except one minor pest finding.",
                "Madam Tan",
                "Restaurant Manager"),
            CancellationToken.None);

        var task = await db.AuditTasks.SingleAsync(row => row.Id == taskId);
        Assert.Equal(AuditTaskStatus.Completed, task.Status);
        Assert.Equal(83.33m, task.AuditScore);
        // Final Review Score waits for the Final Points (D-06).
        Assert.Equal(0m, task.FinalReviewScore);
        Assert.NotNull(task.EndAuditDate);
        Assert.NotNull(task.TimeOut);
        Assert.Equal("All criteria met except one minor pest finding.", task.Summary);
        Assert.Equal("Madam Tan", task.AuditeeName);
        Assert.Equal("Restaurant Manager", task.AuditeeDesignation);
    }

    [Fact]
    public async Task Handle_PerfectScore_IsOneHundred()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.InProgress);
        await AnswerAllItemsAsync(db, taskId, [2m, 3m, 1m]);

        await Handler(db, user).Handle(
            new SubmitAuditTaskCommand(taskId, "Perfect.", "Mr Ali", "Owner"),
            CancellationToken.None);

        var task = await db.AuditTasks.SingleAsync(row => row.Id == taskId);
        Assert.Equal(100.00m, task.AuditScore);
    }

    [Fact]
    public async Task Handle_UnansweredRow_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.InProgress);
        // Only two of the three rows answered.
        await AnswerAllItemsAsync(db, taskId, [2m, 3m, null]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, user).Handle(
                new SubmitAuditTaskCommand(taskId, "s", "n", "d"), CancellationToken.None));

        Assert.Equal(
            "All checklist rows must be answered before submitting.", exception.Message);
    }

    [Fact]
    public async Task Handle_NotStartedTask_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(db, Guid.Parse(user.UserId));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new SubmitAuditTaskCommand(taskId, "s", "n", "d"), CancellationToken.None));

        Assert.Equal("The audit has not been started yet.", exception.Message);
    }

    [Fact]
    public async Task Handle_SecondSubmit_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.Parse(user.UserId), AuditTaskStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new SubmitAuditTaskCommand(taskId, "s", "n", "d"), CancellationToken.None));

        Assert.Equal("The audit has already been submitted.", exception.Message);
    }

    [Fact]
    public async Task Handle_ForeignTask_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, _, _, _, _) = await SeedStandardAuditAsync(
            db, Guid.NewGuid(), AuditTaskStatus.InProgress);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new SubmitAuditTaskCommand(taskId, "s", "n", "d"), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingSummary_Fails(string? summary)
    {
        var results = await new SubmitAuditTaskValidator().ValidateAsync(
            new SubmitAuditTaskCommand(Guid.NewGuid(), summary!, "n", "d"));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Summary is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingAuditeeName_Fails(string? name)
    {
        var results = await new SubmitAuditTaskValidator().ValidateAsync(
            new SubmitAuditTaskCommand(Guid.NewGuid(), "s", name!, "d"));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Auditee Name is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingAuditeeDesignation_Fails(string? designation)
    {
        var results = await new SubmitAuditTaskValidator().ValidateAsync(
            new SubmitAuditTaskCommand(Guid.NewGuid(), "s", "n", designation!));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors,
            error => error.ErrorMessage == "Auditee Designation is required.");
    }
}
