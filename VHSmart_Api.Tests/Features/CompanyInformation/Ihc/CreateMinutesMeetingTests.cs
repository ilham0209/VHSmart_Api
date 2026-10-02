using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class CreateMinutesMeetingTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresMeetingWithAuditStamp()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);

        var response = await new CreateMinutesMeetingHandler(db, user)
            .Handle(MinutesMeetingTestData.ValidCreateCommand(
                "IHC Monthly Meeting",
                new DateTime(2026, 4, 15),
                new TimeOnly(14, 30),
                new TimeOnly(16, 0),
                "HQ Level 3"),
                CancellationToken.None);

        Assert.Equal("IHC Monthly Meeting", response.Title);
        Assert.Equal(new DateTime(2026, 4, 15), response.MeetingDate);
        Assert.Equal(new TimeOnly(14, 30), response.StartTime);
        Assert.Equal(new TimeOnly(16, 0), response.EndTime);
        Assert.Equal("HQ Level 3", response.Location);
        var stored = await db.MinutesMeetings.AsNoTracking().SingleAsync();
        Assert.Equal(user.CompanyId, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
    }

    [Fact]
    public async Task Handle_SameTitleTwice_IsAllowed()
    {
        // Database.md 4 defines no unique index for ComMinutesMeetings and spec 21.9 does not
        // list the meeting title - so unlike trainings, a repeated title is legal.
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        await MinutesMeetingTestData.SeedMeetingAsync(db, user.CompanyId, "Recurring agenda");

        var response = await new CreateMinutesMeetingHandler(db, user)
            .Handle(MinutesMeetingTestData.ValidCreateCommand("Recurring agenda"),
                CancellationToken.None);

        Assert.Equal("Recurring agenda", response.Title);
        Assert.Equal(2, await db.MinutesMeetings.CountAsync());
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fail()
    {
        var validator = new CreateMinutesMeetingValidator();

        var noTitle = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(title: " "));
        var noDate = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(date: default(DateTime)));
        var noStart = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand() with { StartTime = null });
        var noEnd = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand() with { EndTime = null });
        var noLocation = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(location: " "));

        Assert.False(noTitle.IsValid);
        Assert.Contains(
            noTitle.Errors, failure => failure.ErrorMessage == "Title is required.");
        Assert.False(noDate.IsValid);
        Assert.Contains(
            noDate.Errors, failure => failure.ErrorMessage == "Meeting date is required.");
        Assert.False(noStart.IsValid);
        Assert.Contains(
            noStart.Errors, failure => failure.ErrorMessage == "Start time is required.");
        Assert.False(noEnd.IsValid);
        Assert.Contains(
            noEnd.Errors, failure => failure.ErrorMessage == "End time is required.");
        Assert.False(noLocation.IsValid);
        Assert.Contains(
            noLocation.Errors, failure => failure.ErrorMessage == "Location is required.");
    }

    [Fact]
    public async Task Validator_EndBeforeStart_FailsWithD14Message()
    {
        // The legacy data quirk of spec 7.6: 18:32 to 15:35 - D-14 rejects it.
        var validator = new CreateMinutesMeetingValidator();

        var result = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(
                start: new TimeOnly(18, 32), end: new TimeOnly(15, 35)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "End time must be later than start time.");
    }

    [Fact]
    public async Task Validator_EndEqualsStart_IsAllowed()
    {
        // D-14 rejects only a start later than the end; equal times pass.
        var validator = new CreateMinutesMeetingValidator();

        var result = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(
                start: new TimeOnly(9, 0), end: new TimeOnly(9, 0)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_TitleLongerThan200_Fails()
    {
        var validator = new CreateMinutesMeetingValidator();

        var result = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidCreateCommand(title: new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Title must be 200 characters or fewer.");
    }
}
