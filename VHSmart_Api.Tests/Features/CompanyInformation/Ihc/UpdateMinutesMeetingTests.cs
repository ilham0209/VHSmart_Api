using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class UpdateMinutesMeetingTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesFields()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, user.CompanyId);

        var response = await new UpdateMinutesMeetingHandler(db, user)
            .Handle(MinutesMeetingTestData.ValidUpdateCommand(
                meetingId,
                "Renamed meeting",
                new DateTime(2026, 7, 1),
                new TimeOnly(8, 30),
                new TimeOnly(10, 0),
                "Plant 2"),
                CancellationToken.None);

        Assert.Equal("Renamed meeting", response.Title);
        Assert.Equal(new DateTime(2026, 7, 1), response.MeetingDate);
        Assert.Equal(new TimeOnly(8, 30), response.StartTime);
        Assert.Equal(new TimeOnly(10, 0), response.EndTime);
        Assert.Equal("Plant 2", response.Location);
        var stored = await db.MinutesMeetings.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed meeting", stored.Title);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignMeeting_ThrowsNotFoundAndChangesNothing()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignMeeting = await MinutesMeetingTestData.SeedMeetingAsync(
            db, foreignCompany, "Foreign meeting");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMinutesMeetingHandler(db, user)
                .Handle(MinutesMeetingTestData.ValidUpdateCommand(Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateMinutesMeetingHandler(db, user)
                .Handle(MinutesMeetingTestData.ValidUpdateCommand(foreignMeeting),
                    CancellationToken.None));

        var untouched = await db.MinutesMeetings.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == foreignMeeting);
        Assert.Equal("Foreign meeting", untouched.Title);
        Assert.Null(untouched.SysUserModified);
    }

    [Fact]
    public async Task Validator_EndBeforeStart_FailsWithD14Message()
    {
        var validator = new UpdateMinutesMeetingValidator();

        var result = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidUpdateCommand(
                Guid.NewGuid(),
                start: new TimeOnly(18, 32),
                end: new TimeOnly(15, 35)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "End time must be later than start time.");
    }

    [Fact]
    public async Task Validator_MissingFields_Fail()
    {
        var validator = new UpdateMinutesMeetingValidator();
        var id = Guid.NewGuid();

        var noTitle = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidUpdateCommand(id, title: " "));
        var noDate = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidUpdateCommand(id, date: default(DateTime)));
        var noStart = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidUpdateCommand(id) with { StartTime = null });
        var noLocation = await validator.ValidateAsync(
            MinutesMeetingTestData.ValidUpdateCommand(id, location: " "));

        Assert.False(noTitle.IsValid);
        Assert.Contains(
            noTitle.Errors, failure => failure.ErrorMessage == "Title is required.");
        Assert.False(noDate.IsValid);
        Assert.Contains(
            noDate.Errors, failure => failure.ErrorMessage == "Meeting date is required.");
        Assert.False(noStart.IsValid);
        Assert.Contains(
            noStart.Errors, failure => failure.ErrorMessage == "Start time is required.");
        Assert.False(noLocation.IsValid);
        Assert.Contains(
            noLocation.Errors, failure => failure.ErrorMessage == "Location is required.");
    }
}
