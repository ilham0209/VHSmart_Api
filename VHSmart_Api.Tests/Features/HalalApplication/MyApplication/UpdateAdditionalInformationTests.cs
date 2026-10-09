using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class UpdateAdditionalInformationTests
{
    private static AdditionalInfoItemInput Item(
        ApplicationAdditionalInfoSection section,
        string optionCode,
        string? freeText = null) =>
        new(section, optionCode, freeText);

    [Fact]
    public async Task Handle_ValidCommand_ReplacesTheStoredRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.Packaging, "BOTTLE");
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.QualityControl, "HACCP");

        var response = await new UpdateAdditionalInformationHandler(db, user)
            .Handle(
                new UpdateAdditionalInformationCommand(
                    applicationId,
                    [
                        Item(ApplicationAdditionalInfoSection.Packaging,
                            "CARTON_BOX"),
                        Item(ApplicationAdditionalInfoSection.QualityControl,
                            "OTHERS", "Internal audit yearly")
                    ]),
                CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.Equal("CARTON_BOX", response[0].OptionCode);
        Assert.Equal("OTHERS", response[1].OptionCode);
        Assert.Equal("Internal audit yearly", response[1].FreeText);

        var live = await db.ApplicationAdditionalInfoItems
            .Where(row => row.ApplicationId == applicationId)
            .ToListAsync();
        Assert.Equal(2, live.Count);
        Assert.DoesNotContain(live, row => row.OptionCode == "BOTTLE");

        // The unticked rows were soft-deleted, never physically removed (CodingRules 7.2).
        var removed = await db.ApplicationAdditionalInfoItems
            .IgnoreQueryFilters()
            .Where(row => row.ApplicationId == applicationId)
            .ToListAsync();
        Assert.Equal(4, removed.Count);
        Assert.Equal(2, removed.Count(row => row.IsDeleted));
    }

    [Fact]
    public async Task Handle_SameOptionInBothSections_StoresBoth()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new UpdateAdditionalInformationHandler(db, user)
            .Handle(
                new UpdateAdditionalInformationCommand(
                    applicationId,
                    [
                        Item(ApplicationAdditionalInfoSection.Packaging, "OTHERS",
                            "Glass jar"),
                        Item(ApplicationAdditionalInfoSection.QualityControl,
                            "OTHERS", "Third party audit")
                    ]),
                CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.Equal(2, await db.ApplicationAdditionalInfoItems
            .CountAsync(row => row.ApplicationId == applicationId));
    }

    [Fact]
    public async Task Handle_EmptyList_ClearsTheTab()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.Packaging, "BOTTLE");

        var response = await new UpdateAdditionalInformationHandler(db, user)
            .Handle(
                new UpdateAdditionalInformationCommand(applicationId, []),
                CancellationToken.None);

        Assert.Empty(response);
        Assert.Empty(await db.ApplicationAdditionalInfoItems
            .Where(row => row.ApplicationId == applicationId)
            .ToListAsync());
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new UpdateAdditionalInformationHandler(db, user)
                .Handle(
                    new UpdateAdditionalInformationCommand(foreignId, []),
                    CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_SubmittedApplication_ThrowsTheD26ReadOnlyRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, status: ApplicationStatus.AuditCompleted);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new UpdateAdditionalInformationHandler(db, user)
                .Handle(
                    new UpdateAdditionalInformationCommand(
                        applicationId,
                        [Item(ApplicationAdditionalInfoSection.Packaging, "BOTTLE")]),
                    CancellationToken.None));

        Assert.Equal(
            "Submitted applications cannot be edited except for status tagging.",
            exception.Message);
        Assert.Equal(0, await db.ApplicationAdditionalInfoItems.CountAsync());
    }

    [Fact]
    public async Task Validator_ValidItems_Pass()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [
                    Item(ApplicationAdditionalInfoSection.Packaging, "CARTON_BOX"),
                    Item(ApplicationAdditionalInfoSection.QualityControl,
                        "MS_ISO", "ISO 22000")
                ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_UnknownOptionCode_Fails()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [Item(ApplicationAdditionalInfoSection.Packaging, "WOODEN_CRATE")]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Additional information option is not valid for its section.");
    }

    [Fact]
    public async Task Validator_DuplicateSectionAndCode_Fails()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [
                    Item(ApplicationAdditionalInfoSection.Packaging, "BOTTLE"),
                    Item(ApplicationAdditionalInfoSection.Packaging, "BOTTLE")
                ]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Additional information option is duplicated.");
    }

    [Fact]
    public async Task Validator_SameCodeAcrossSections_Passes()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [
                    Item(ApplicationAdditionalInfoSection.Packaging, "OTHERS"),
                    Item(ApplicationAdditionalInfoSection.QualityControl, "OTHERS")
                ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_MissingItems_Fails()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(Guid.NewGuid(), null!));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Additional information items are required.");
    }

    [Fact]
    public async Task Validator_MissingOptionCode_Fails()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [Item(ApplicationAdditionalInfoSection.Packaging, "")]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Additional information option is required.");
    }

    [Fact]
    public async Task Validator_FreeTextTooLong_Fails()
    {
        var validator = new UpdateAdditionalInformationValidator();

        var result = await validator.ValidateAsync(
            new UpdateAdditionalInformationCommand(
                Guid.NewGuid(),
                [Item(ApplicationAdditionalInfoSection.Packaging, "OTHERS",
                    new string('x', 501))]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Additional information text must be 500 characters or fewer.");
    }
}
