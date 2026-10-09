using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class CreateApplicationTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesDraftWithD09ReferenceAndHistory()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await ProductSchemeIdAsync(db);
        var command = CorrectSurveyCommand(schemeId);

        var response = await new CreateApplicationHandler(db, user, CreateGenerator(db))
            .Handle(command, CancellationToken.None);

        var stored = await db.Applications.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(ApplicationStatus.Draft, stored.Status);
        Assert.Equal("New", stored.ApplicationType);
        Assert.Equal(schemeId, stored.SchemeId);
        Assert.True(stored.SurveyReadProcedureManual);
        Assert.True(stored.SurveyReadMs1500);
        Assert.False(stored.SurveyHandlesProhibited);
        Assert.True(stored.SurveyHasIhc);
        Assert.Null(stored.BatchId);

        // D-09: {Prefix}({SchemeCode})/{ddMMyyyy}/{n} with the placeholder prefix "VHS"
        // and the PR scheme code; today's date, first number of the day.
        Assert.Matches(@"^VHS\(PR\)/\d{8}/1$", stored.ReferenceNo);
        Assert.Null(stored.CbApplicationNo);

        // D-26: creation writes the first history row - no FromStatus before DRAFT.
        var history = await db.ApplicationStatusHistories.SingleAsync();
        Assert.Equal(CompanyA, history.CompanyId);
        Assert.Equal(stored.Id, history.ApplicationId);
        Assert.Null(history.FromStatus);
        Assert.Equal(ApplicationStatus.Draft, history.ToStatus);
        Assert.Equal(Guid.Parse(user.UserId), history.ChangedBy);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal(stored.ReferenceNo, response.ReferenceNo);
        Assert.Equal(ApplicationStatus.Draft, response.Status);
        Assert.Equal("New", response.ApplicationType);
        Assert.Equal(schemeId, response.SchemeId);
        Assert.NotNull(response.SchemeName);
        Assert.Null(response.BatchId);
        Assert.Null(response.BatchName);
    }

    [Fact]
    public async Task Handle_RenewalType_IsStoredVerbatim()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = (await ValidCommandAsync(db)) with { ApplicationType = "Renewal" };

        var response = await new CreateApplicationHandler(db, user, CreateGenerator(db))
            .Handle(command, CancellationToken.None);

        Assert.Equal("Renewal", response.ApplicationType);
        Assert.Equal("Renewal", (await db.Applications.SingleAsync()).ApplicationType);
    }

    [Fact]
    public async Task Handle_LowerCaseType_IsNormalizedToTheDatabaseCasing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = (await ValidCommandAsync(db)) with { ApplicationType = "renewal" };

        var response = await new CreateApplicationHandler(db, user, CreateGenerator(db))
            .Handle(command, CancellationToken.None);

        Assert.Equal("Renewal", response.ApplicationType);
    }

    [Theory]
    [InlineData(false, true, false, true)] // Q1 No
    [InlineData(true, false, false, true)] // Q2 No
    [InlineData(true, true, true, true)]   // Q3 Yes - handles prohibited goods
    [InlineData(true, true, false, false)] // Q4 No
    public async Task Handle_WrongSurveyAnswer_ThrowsBusinessRuleWithTheD08Message(
        bool readProcedureManual,
        bool readMs1500,
        bool handlesProhibited,
        bool hasIhc)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await ProductSchemeIdAsync(db);
        var command = new CreateApplicationCommand(
            schemeId, null, readProcedureManual, readMs1500, handlesProhibited, hasIhc);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new CreateApplicationHandler(db, user, CreateGenerator(db))
                .Handle(command, CancellationToken.None));

        Assert.Equal(
            "Your answers do not allow you to proceed with a Halal application.",
            exception.Message);
        Assert.Equal(0, await db.Applications.CountAsync());
        Assert.Equal(0, await db.ApplicationStatusHistories.CountAsync());
    }

    [Fact]
    public async Task Handle_SchemeWithoutCode_ThrowsBusinessRuleRejection()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = CorrectSurveyCommand(await SchemeWithoutCodeIdAsync(db));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new CreateApplicationHandler(db, user, CreateGenerator(db))
                .Handle(command, CancellationToken.None));

        // Text is ours; it mirrors D-09's "Audit Prefix is not set for brand {name}".
        Assert.Equal("Scheme code is not set for scheme Abattoirs", exception.Message);
        Assert.Equal(0, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownScheme_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = CorrectSurveyCommand(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new CreateApplicationHandler(db, user, CreateGenerator(db))
                .Handle(command, CancellationToken.None));

        Assert.Equal("Scheme not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_SchemeIsGlobal_CreatesTheRowInTheCallersTenant()
    {
        // Schemes are [G] rows (Database.md 14): company B's user creates with the same
        // scheme and lands in their own tenant.
        var user = UserB();
        var db = await CreateDbAsync(user);
        var command = await ValidCommandAsync(db);

        var response = await new CreateApplicationHandler(db, user, CreateGenerator(db))
            .Handle(command, CancellationToken.None);

        var stored = await db.Applications.SingleAsync();
        Assert.Equal(CompanyB, stored.CompanyId);
        Assert.Equal(response.Id, stored.Id);
    }

    [Fact]
    public async Task Handle_SecondApplicationSameDay_IncrementsTheRunningNumber()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await ValidCommandAsync(db);
        var handler = new CreateApplicationHandler(db, user, CreateGenerator(db));

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Matches(@"^VHS\(PR\)/\d{8}/1$", first.ReferenceNo);
        Assert.Matches(@"^VHS\(PR\)/\d{8}/2$", second.ReferenceNo);
        Assert.Equal(2, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);

        var result = await validator.ValidateAsync(await ValidCommandAsync(db));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingScheme_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);
        var command = (await ValidCommandAsync(db)) with { SchemeId = Guid.Empty };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme is required.");
    }

    [Fact]
    public async Task Validator_UnknownScheme_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);
        var command = (await ValidCommandAsync(db)) with { SchemeId = Guid.NewGuid() };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme not found.");
    }

    [Fact]
    public async Task Validator_UnknownApplicationType_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);
        var command = (await ValidCommandAsync(db)) with { ApplicationType = "RENEW" };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Application type must be New or Renewal.");
    }

    [Fact]
    public async Task Validator_ApplicationTypeTooLong_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);
        var command = (await ValidCommandAsync(db)) with
        {
            ApplicationType = new string('x', 21)
        };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Application type must be 20 characters or fewer.");
    }

    [Fact]
    public async Task Validator_NullApplicationType_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateApplicationValidator(db);
        var command = await ValidCommandAsync(db);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_CreatedRow_KeepsTheReferenceUniqueAcrossCompanies()
    {
        // UQ ReferenceNo (Database.md 10): the generator's scope carries no company, so two
        // companies on the same day still receive different numbers.
        var userA = UserA();
        var userB = UserB();
        var db = await CreateDbAsync(userA);
        var command = await ValidCommandAsync(db);

        var first = await new CreateApplicationHandler(db, userA, CreateGenerator(db))
            .Handle(command, CancellationToken.None);
        var second = await new CreateApplicationHandler(db, userB, CreateGenerator(db))
            .Handle(command, CancellationToken.None);

        Assert.NotEqual(first.ReferenceNo, second.ReferenceNo);
        Assert.Equal(2, await db.Applications.IgnoreQueryFilters().CountAsync());
    }
}
