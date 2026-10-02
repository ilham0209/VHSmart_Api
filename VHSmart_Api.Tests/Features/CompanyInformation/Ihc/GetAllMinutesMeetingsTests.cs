using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class GetAllMinutesMeetingsTests
{
    [Fact]
    public async Task Handle_DefaultSort_IsNewestFirstWithCompany()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        await MinutesMeetingTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        await MinutesMeetingTestData.SeedMeetingAsync(
            db, user.CompanyId, "Older meeting", date: new DateTime(2026, 1, 10));
        await MinutesMeetingTestData.SeedMeetingAsync(
            db, user.CompanyId, "Newer meeting", date: new DateTime(2026, 6, 20));

        var grid = await new GetAllMinutesMeetingsHandler(db, user)
            .Handle(new GetAllMinutesMeetingsQuery(), CancellationToken.None);

        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Newer meeting", "Older meeting" }, rows.Select(row => row.Title));
        Assert.Equal("Verify Halal Sdn Bhd", rows[0].Company);
        Assert.Equal(new TimeOnly(9, 0), rows[0].StartTime);
        Assert.Equal(new TimeOnly(11, 0), rows[0].EndTime);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsTitleOrLocation()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        await MinutesMeetingTestData.SeedMeetingAsync(
            db, user.CompanyId, "Monthly IHC session");
        await MinutesMeetingTestData.SeedMeetingAsync(
            db, user.CompanyId, "Audit prep", location: "Plant canteen");

        var byTitle = await new GetAllMinutesMeetingsHandler(db, user)
            .Handle(new GetAllMinutesMeetingsQuery
            {
                Request = new DataGridRequest { SearchTerm = "monthly" }
            }, CancellationToken.None);
        var byLocation = await new GetAllMinutesMeetingsHandler(db, user)
            .Handle(new GetAllMinutesMeetingsQuery
            {
                Request = new DataGridRequest { SearchTerm = "canteen" }
            }, CancellationToken.None);

        Assert.Equal("Monthly IHC session", Assert.Single(byTitle.Data).Title);
        Assert.Equal("Audit prep", Assert.Single(byLocation.Data).Title);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = MinutesMeetingTestData.CompanyUser();
        var userB = MinutesMeetingTestData.CompanyUser();
        var databaseName = TestDbFactory.NewDatabaseName();
        var dbA = TestDbFactory.Create(databaseName, userA);
        await dbA.Database.EnsureCreatedAsync();
        var companyA = await MinutesMeetingTestData.SeedCompanyAsync(
            dbA, "Company A", userA.CompanyId);
        var companyB = await MinutesMeetingTestData.SeedCompanyAsync(
            dbA, "Company B", userB.CompanyId);
        await MinutesMeetingTestData.SeedMeetingAsync(dbA, companyA, "Company A meeting");
        await MinutesMeetingTestData.SeedMeetingAsync(dbA, companyB, "Company B meeting");

        var asCompanyA = await new GetAllMinutesMeetingsHandler(dbA, userA)
            .Handle(new GetAllMinutesMeetingsQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllMinutesMeetingsHandler(
                TestDbFactory.Create(databaseName, userB), userB)
            .Handle(new GetAllMinutesMeetingsQuery(), CancellationToken.None);

        Assert.Equal("Company A meeting", Assert.Single(asCompanyA.Data).Title);
        Assert.Equal("Company B meeting", Assert.Single(asCompanyB.Data).Title);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = MinutesMeetingTestData.CompanyUser();
        var db = await MinutesMeetingTestData.CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(
            db, user.CompanyId, "Doomed meeting");
        var meeting = await db.MinutesMeetings.SingleAsync(row => row.Id == meetingId);
        db.MinutesMeetings.Remove(meeting);
        await db.SaveChangesAsync();

        var grid = await new GetAllMinutesMeetingsHandler(db, user)
            .Handle(new GetAllMinutesMeetingsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }
}
