using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class UpdateApplicationTests
{
    private static UpdateApplicationCommand Command(
        Guid id,
        string applicationType = "New",
        string? cbApplicationNo = null,
        DateOnly? cbApplicationDate = null,
        string? halalCoachName = null,
        Guid? batchId = null) =>
        new(id, applicationType, cbApplicationNo, cbApplicationDate, halalCoachName, batchId);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesTheHeaderBlock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);

        var response = await new UpdateApplicationHandler(db, user)
            .Handle(
                Command(
                    applicationId,
                    applicationType: "renewal",
                    cbApplicationNo: "CB-2026-001",
                    cbApplicationDate: new DateOnly(2026, 10, 1),
                    halalCoachName: "Coach Rahim",
                    batchId: batchId),
                CancellationToken.None);

        Assert.Equal(applicationId, response.Id);
        Assert.Equal(ApplicationStatus.Draft, response.Status);
        Assert.Equal("Renewal", response.ApplicationType);
        Assert.Equal("CB-2026-001", response.CbApplicationNo);
        Assert.Equal(new DateOnly(2026, 10, 1), response.CbApplicationDate);
        Assert.Equal("Coach Rahim", response.HalalCoachName);
        Assert.Equal(batchId, response.BatchId);
        Assert.Equal("Santan Batch Pertama", response.BatchName);

        var stored = await db.Applications.SingleAsync(row => row.Id == applicationId);
        Assert.Equal("Renewal", stored.ApplicationType);
        Assert.Equal("CB-2026-001", stored.CbApplicationNo);
        Assert.Equal(batchId, stored.BatchId);
    }

    [Fact]
    public async Task Handle_NullBatch_ClearsTheLink()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var response = await new UpdateApplicationHandler(db, user)
            .Handle(Command(applicationId, batchId: null), CancellationToken.None);

        Assert.Null(response.BatchId);
        Assert.Null(response.BatchName);
        Assert.Null((await db.Applications.SingleAsync()).BatchId);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new UpdateApplicationHandler(db, user)
                .Handle(Command(foreignId), CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_SubmittedApplication_ThrowsTheD26ReadOnlyRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, status: ApplicationStatus.AuditInProgress);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new UpdateApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        // Our message (the spec gives none): D-26 "Submitted applications are read-only".
        Assert.Equal(
            "Submitted applications cannot be edited except for status tagging.",
            exception.Message);
        Assert.Null((await db.Applications.SingleAsync()).CbApplicationNo);
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_TypeIsCaseInsensitive_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), applicationType: "RENEWAL"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UnknownApplicationType_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), applicationType: "RENEW"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Application type must be New or Renewal.");
    }

    [Fact]
    public async Task Validator_ForeignBatch_FailsWithTheBatchMessage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignBatchId = await SeedBatchAsync(db, CompanyB);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), batchId: foreignBatchId));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Batch not found.");
    }

    [Fact]
    public async Task Validator_OwnBatch_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), batchId: batchId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_CbApplicationNoTooLong_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateApplicationValidator(db, user);

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), cbApplicationNo: new string('x', 101)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "CB application number must be 100 characters or fewer.");
    }
}
