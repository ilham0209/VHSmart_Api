using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

// "Tagging Application Status" (spec 12.7 [MANUAL], D-26): the four CB-progress statuses
// forward-only ("in that order or any later one"), the D-26 history row on every change and
// the read-only rules around it (tagging is an exception; DRAFT is not taggable).
public class TagApplicationStatusTests
{
    private static TagApplicationStatusCommand Command(
        Guid id,
        string status = ApplicationStatus.ApplicationApproved,
        string? remarks = null) =>
        new(id, status, remarks);

    private const string Processing = "PROCESSING AT JAKIM (NEW)";

    private static async Task<(TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId)>
        SubmittedApplicationAsync(string status = Processing) =>
        await SeededAsync(CompanyA, status);

    private static async Task<(TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId)>
        SeededAsync(Guid companyId, string status)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, companyId);
        var applicationId = await SeedApplicationAsync(db, companyId, status: status);
        return (db, user, applicationId);
    }

    [Fact]
    public async Task Handle_FromProcessing_SetsApplicationApprovedWithHistoryRow()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync();

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(
                Command(applicationId, remarks: "CB confirmed the audit date."),
                CancellationToken.None);

        Assert.Equal(applicationId, response.Id);
        Assert.Equal(ApplicationStatus.ApplicationApproved, response.Status);

        var stored = await db.Applications.SingleAsync();
        Assert.Equal(ApplicationStatus.ApplicationApproved, stored.Status);
        Assert.True(stored.StatusDate > DateTime.UtcNow.AddMinutes(-5));

        var history = await db.ApplicationStatusHistories.SingleAsync();
        Assert.Equal(applicationId, history.ApplicationId);
        Assert.Equal(Processing, history.FromStatus);
        Assert.Equal(ApplicationStatus.ApplicationApproved, history.ToStatus);
        Assert.Equal("CB confirmed the audit date.", history.Remarks);
        Assert.Equal(Guid.Parse(user.UserId!), history.ChangedBy);
    }

    [Fact]
    public async Task Handle_SkipAheadToAnyLaterStatus_IsAllowed()
    {
        // "or any later one" (D-26): from Processing straight to Audit (Completed).
        var (db, user, applicationId) = await SubmittedApplicationAsync();

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId, ApplicationStatus.AuditCompleted),
                CancellationToken.None);

        Assert.Equal(ApplicationStatus.AuditCompleted, response.Status);
    }

    [Fact]
    public async Task Handle_ForwardWithinTheFour_Succeeds()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync(
            ApplicationStatus.ApplicationApproved);

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId, ApplicationStatus.AuditInProgress),
                CancellationToken.None);

        Assert.Equal(ApplicationStatus.AuditInProgress, response.Status);
        Assert.Equal(
            ApplicationStatus.ApplicationApproved,
            (await db.ApplicationStatusHistories.SingleAsync()).FromStatus);
    }

    [Fact]
    public async Task Handle_BackwardsTarget_ThrowsTheForwardRule()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync(
            ApplicationStatus.AuditInProgress);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new TagApplicationStatusHandler(db, user)
                .Handle(Command(applicationId, ApplicationStatus.ApplicationApproved),
                    CancellationToken.None));

        Assert.Equal("Application status can only move forward.", exception.Message);
        Assert.Equal(
            ApplicationStatus.AuditInProgress,
            (await db.Applications.SingleAsync()).Status);
        Assert.Equal(0, await db.ApplicationStatusHistories.CountAsync());
    }

    [Fact]
    public async Task Handle_FromDraft_ThrowsTheSubmitFirstRule()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync(
            ApplicationStatus.Draft);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new TagApplicationStatusHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal(
            "Submit the application before tagging its status.", exception.Message);
        Assert.Equal(ApplicationStatus.Draft, (await db.Applications.SingleAsync()).Status);
    }

    [Fact]
    public async Task Handle_LegacyStatusOutsideTheD26Set_Throws()
    {
        // "Payment Confirmed" appears in the legacy data but D-26 does not name it - it
        // cannot be tagged (flagged stance).
        var (db, user, applicationId) = await SubmittedApplicationAsync("Payment Confirmed");

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new TagApplicationStatusHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal("This application status cannot be tagged.", exception.Message);
    }

    [Fact]
    public async Task Handle_SameStatusAgain_SavesAndWritesHistory()
    {
        // Re-tagging the current status is a Save without a move - allowed, and the history
        // row records the confirmation (spec silent - flagged).
        var (db, user, applicationId) = await SubmittedApplicationAsync(
            ApplicationStatus.ApplicationApproved);

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId, ApplicationStatus.ApplicationApproved),
                CancellationToken.None);

        Assert.Equal(ApplicationStatus.ApplicationApproved, response.Status);
        var history = await db.ApplicationStatusHistories.SingleAsync();
        Assert.Equal(ApplicationStatus.ApplicationApproved, history.FromStatus);
        Assert.Equal(ApplicationStatus.ApplicationApproved, history.ToStatus);
    }

    [Fact]
    public async Task Handle_TargetSentCaseIsNormalized()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync();

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId, "audit (completed)"), CancellationToken.None);

        Assert.Equal(ApplicationStatus.AuditCompleted, response.Status);
        Assert.Equal(
            ApplicationStatus.AuditCompleted,
            (await db.ApplicationStatusHistories.SingleAsync()).ToStatus);
    }

    [Fact]
    public async Task Handle_WithoutRemarks_StoresNull()
    {
        var (db, user, applicationId) = await SubmittedApplicationAsync();

        await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        Assert.Null((await db.ApplicationStatusHistories.SingleAsync()).Remarks);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var (db, user, _) = await SubmittedApplicationAsync();
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new TagApplicationStatusHandler(db, user)
                .Handle(Command(foreignApplicationId), CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_TaggingIsAllowedAfterSubmit_TheD26Exception()
    {
        // No EnsureDraft: tagging is one of the three D-26 read-only exceptions.
        var (db, user, applicationId) = await SubmittedApplicationAsync(
            ApplicationStatus.ApprovedWithDocument);

        var response = await new TagApplicationStatusHandler(db, user)
            .Handle(Command(applicationId, ApplicationStatus.ApprovedWithDocument),
                CancellationToken.None);

        Assert.Equal(ApplicationStatus.ApprovedWithDocument, response.Status);
    }

    [Fact]
    public async Task Validator_AllFourStatuses_Pass()
    {
        var validator = new TagApplicationStatusValidator();
        string[] statuses =
        [
            ApplicationStatus.ApplicationApproved,
            ApplicationStatus.AuditInProgress,
            ApplicationStatus.AuditCompleted,
            ApplicationStatus.ApprovedWithDocument
        ];

        foreach (var status in statuses)
        {
            var result = await validator.ValidateAsync(Command(Guid.NewGuid(), status));
            Assert.True(result.IsValid, status);
        }
    }

    [Fact]
    public async Task Validator_EmptyStatus_Fails()
    {
        var validator = new TagApplicationStatusValidator();

        var result = await validator.ValidateAsync(Command(Guid.NewGuid(), ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Status is required.");
    }

    [Fact]
    public async Task Validator_StatusOutsideTheFour_Fails()
    {
        var validator = new TagApplicationStatusValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), "Payment Confirmed"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Status must be Application Approved, Audit (In Progress), "
                    + "Audit (Completed) or Approved with Document.");
    }

    [Fact]
    public async Task Validator_RemarksTooLong_Fails()
    {
        var validator = new TagApplicationStatusValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), remarks: new string('x', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Remarks must be 500 characters or fewer.");
    }
}
