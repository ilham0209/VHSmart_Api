using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

// Submit Application (spec 12.5 tab / 12.6 prerequisites): the batch + CB gates, the D-18 /
// Q10 raw-material block, the acknowledgement validation, the Draft -> Processing status
// with its D-26 history row and the CB-name status string (spec 12.7).
public class SubmitApplicationTests
{
    private static SubmitApplicationCommand Command(
        Guid id,
        string ackName = "Ahmad bin Ali",
        string ackEmail = "ahmad@example.com",
        string ackMobile = "+60123456789",
        bool ackAccepted = true) =>
        new(id, ackName, ackEmail, ackMobile, ackAccepted);

    // A ready-to-submit setup: own company with its CB (the status carries its name), an
    // application with CB number/date and a Food Premise batch holding one premise - the
    // lightest happy path (no products, so no D-18 rows to build).
    private static async Task<(TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId)>
        ReadyApplicationAsync(string cbName = "JAKIM")
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        await SeedCertificationBodyAsync(db, CompanyA, cbName);

        var batchId = await BatchTestData.SeedBatchAsync(
            db, CompanyA, schemeId: await BatchTestData.FoodPremiseSchemeIdAsync(db));
        var premiseId = await BatchTestData.SeedCompletePremiseAsync(db, CompanyA);
        await BatchTestData.SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);

        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));
        return (db, user, applicationId);
    }

    [Fact]
    public async Task Handle_FoodPremiseBatch_SubmitsWithTheCbProcessingStatus()
    {
        var (db, user, applicationId) = await ReadyApplicationAsync();

        var response = await new SubmitApplicationHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        Assert.Equal(applicationId, response.Id);
        Assert.Equal("PROCESSING AT JAKIM (NEW)", response.Status);
        Assert.NotNull(response.SubmittedAt);

        var stored = await db.Applications.SingleAsync();
        Assert.Equal("PROCESSING AT JAKIM (NEW)", stored.Status);
        Assert.True(stored.StatusDate > DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal("Ahmad bin Ali", stored.AckName);
        Assert.Equal("ahmad@example.com", stored.AckEmail);
        Assert.Equal("+60123456789", stored.AckMobile);
        Assert.True(stored.AckAccepted);
        Assert.Equal(Guid.Parse(user.UserId!), stored.SubmittedBy);
    }

    [Fact]
    public async Task Handle_Submit_WritesTheD26HistoryRow()
    {
        var (db, user, applicationId) = await ReadyApplicationAsync();

        await new SubmitApplicationHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        var history = await db.ApplicationStatusHistories.SingleAsync();
        Assert.Equal(applicationId, history.ApplicationId);
        // The transition itself: DRAFT -> Processing (creation wrote the FromStatus=null row).
        Assert.Equal(ApplicationStatus.Draft, history.FromStatus);
        Assert.Equal("PROCESSING AT JAKIM (NEW)", history.ToStatus);
        Assert.Equal(Guid.Parse(user.UserId!), history.ChangedBy);
    }

    [Fact]
    public async Task Handle_ProductBatchWithValidProduct_Submits()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        await SeedCertificationBodyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(db, CompanyA);
        var productId = await BatchTestData.SeedValidProductAsync(db, CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var response = await new SubmitApplicationHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        Assert.Equal("PROCESSING AT JAKIM (NEW)", response.Status);
    }

    [Fact]
    public async Task Handle_ProductWithExpiredCertificate_BlocksByQ10()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(db, CompanyA);
        var productId = await BatchTestData.SeedExpiredProductAsync(db, CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        // D-18 + Q10 (Block): a certificate that expired AFTER the product was linked still
        // blocks the submit.
        Assert.Equal(
            "Batch products must have valid halal raw materials before submission.",
            exception.Message);
        Assert.Equal(ApplicationStatus.Draft,
            (await db.Applications.SingleAsync()).Status);
    }

    [Fact]
    public async Task Handle_ProductWithoutIngredient_BlocksByQ10()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(db, CompanyA);
        var productId = await BatchTestData.SeedUnlinkedProductAsync(db, CompanyA);
        await BatchTestData.SeedBatchProductAsync(db, CompanyA, batchId, productId);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        // Unlinked is not Valid either - D-18 only calls a product Valid from >= 1 linked
        // raw material.
        Assert.Equal(
            "Batch products must have valid halal raw materials before submission.",
            exception.Message);
    }

    [Fact]
    public async Task Handle_NoBatch_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal("A batch must be selected before submission.", exception.Message);
    }

    [Fact]
    public async Task Handle_EmptyBatch_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal("The selected batch has no products or premises.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingCbApplicationNo_Throws()
    {
        var (db, user, _) = await ReadyApplicationAsync();
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: (await db.Applications.Select(row => row.BatchId).FirstAsync()),
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal("CB application number is required.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingCbApplicationDate_Throws()
    {
        var (db, user, _) = await ReadyApplicationAsync();
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: (await db.Applications.Select(row => row.BatchId).FirstAsync()),
            cbApplicationNo: "CB-2026-001");

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(applicationId), CancellationToken.None));

        Assert.Equal("CB application date is required.", exception.Message);
    }

    [Fact]
    public async Task Handle_SubmittedApplication_ThrowsTheD26ReadOnlyRule()
    {
        var (db, user, _) = await ReadyApplicationAsync();
        var submittedId = await SeedApplicationAsync(
            db,
            CompanyA,
            status: "PROCESSING AT JAKIM (NEW)",
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(submittedId), CancellationToken.None));

        Assert.Equal(
            "Submitted applications cannot be edited except for status tagging.",
            exception.Message);
        // The blocked submit wrote no second history row (the seeder writes none at all).
        Assert.Equal(0, await db.ApplicationStatusHistories.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var (db, user, _) = await ReadyApplicationAsync();
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new SubmitApplicationHandler(db, user)
                .Handle(Command(foreignApplicationId), CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingCertificationBodyRow_FallsBackToProcessingAtCb()
    {
        // Data hole: the company's CB row is gone (the FK value points nowhere) - the status
        // must still fit the column (flagged fallback of ProcessingStatus).
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(
            db, CompanyA, schemeId: await BatchTestData.FoodPremiseSchemeIdAsync(db));
        var premiseId = await BatchTestData.SeedCompletePremiseAsync(db, CompanyA);
        await BatchTestData.SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            batchId: batchId,
            cbApplicationNo: "CB-2026-001",
            cbApplicationDate: new DateOnly(2026, 10, 1));

        var response = await new SubmitApplicationHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        Assert.Equal("PROCESSING AT CB (NEW)", response.Status);
    }

    [Fact]
    public async Task Handle_LongCertificationBodyName_FitsTheStatusColumn()
    {
        var longName = new string('X', 80);
        var (db, user, applicationId) = await ReadyApplicationAsync(longName);

        var response = await new SubmitApplicationHandler(db, user)
            .Handle(Command(applicationId), CancellationToken.None);

        // Status is nvarchar(60): 14 + 40 + 6.
        Assert.Equal(60, response.Status.Length);
        Assert.StartsWith("PROCESSING AT XXXX", response.Status);
        Assert.EndsWith(" (NEW)", response.Status);
    }

    [Fact]
    public async Task Validator_ValidAcknowledgement_Passes()
    {
        var validator = new SubmitApplicationValidator();

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingAcknowledgementFields_Fails()
    {
        var validator = new SubmitApplicationValidator();

        var result = await validator.ValidateAsync(Command(
            Guid.NewGuid(), ackName: "", ackEmail: "", ackMobile: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Acknowledgement name is required.");
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Acknowledgement email is required.");
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Acknowledgement mobile number is required.");
    }

    [Fact]
    public async Task Validator_MalformedEmail_Fails()
    {
        var validator = new SubmitApplicationValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), ackEmail: "not-an-email"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "A valid email address is required.");
    }

    [Fact]
    public async Task Validator_CheckboxUnchecked_Fails()
    {
        var validator = new SubmitApplicationValidator();

        var result = await validator.ValidateAsync(Command(Guid.NewGuid(), ackAccepted: false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "The acknowledgement must be accepted.");
    }

    [Fact]
    public async Task Validator_AckMobileTooLong_Fails()
    {
        var validator = new SubmitApplicationValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), ackMobile: new string('1', 31)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage
                == "Acknowledgement mobile number must be 30 characters or fewer.");
    }
}
