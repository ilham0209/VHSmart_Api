using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class DeleteMinutesMeetingTests
{
    [Fact]
    public async Task Handle_SoftDeletesTheMeeting()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, user.CompanyId);

        await new DeleteMinutesMeetingHandler(db, user)
            .Handle(new DeleteMinutesMeetingCommand(meetingId), CancellationToken.None);

        Assert.Equal(0, await db.MinutesMeetings.CountAsync());
        Assert.True(await db.MinutesMeetings.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == meetingId && row.IsDeleted));
    }

    [Fact]
    public async Task Handle_UnknownOrForeignMeeting_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignMeeting = await MinutesMeetingTestData.SeedMeetingAsync(
            db, foreignCompany, "Foreign meeting");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMinutesMeetingHandler(db, user)
                .Handle(new DeleteMinutesMeetingCommand(Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMinutesMeetingHandler(db, user)
                .Handle(new DeleteMinutesMeetingCommand(foreignMeeting),
                    CancellationToken.None));

        Assert.True(await db.MinutesMeetings.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == foreignMeeting && !row.IsDeleted));
    }
}
