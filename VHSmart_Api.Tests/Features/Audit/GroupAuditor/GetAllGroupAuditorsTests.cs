using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class GetAllGroupAuditorsTests
{
    private static GetAllGroupAuditorsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsNameLeaderJoinedMembersAndStatus()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");
        await SeedGroupAsync(
            db, CompanyA, checklist, leader, [leader, second],
            name: "TEAM ALPHA", status: GroupAuditorStatus.Active);

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("TEAM ALPHA", row.GroupName);
        Assert.Equal("LEAD ONE", row.GroupLeaderName);
        // Member names join alphabetically (the AU-03 RM-02 stance; flagged).
        Assert.Equal("AUDITOR TWO, LEAD ONE", row.AuditorName);
        Assert.Equal(GroupAuditorStatus.Active, row.Status);
        Assert.Equal(1, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsGroupNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM ZULU");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM ALPHA");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM MIKE");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery(), CancellationToken.None);

        Assert.Equal(["TEAM ALPHA", "TEAM MIKE", "TEAM ZULU"],
            grid.Data.Select(row => row.GroupName).ToList());
    }

    [Fact]
    public async Task Handle_Search_MatchesGroupName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM ALPHA");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "CREW BRAVO");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery
            {
                Request = new DataGridRequest { SearchTerm = "alpha" }
            },
            CancellationToken.None);

        Assert.Equal("TEAM ALPHA", Assert.Single(grid.Data).GroupName);
        Assert.Equal(1, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_Search_MatchesLeaderName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leadOne = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var leadTwo = await SeedAuditorAsync(db, CompanyA, name: "LEAD TWO");
        await SeedGroupAsync(db, CompanyA, checklist, leadOne, [leadOne], name: "TEAM ALPHA");
        await SeedGroupAsync(db, CompanyA, checklist, leadTwo, [leadTwo], name: "TEAM BRAVO");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery
            {
                Request = new DataGridRequest { SearchTerm = "LEAD TWO" }
            },
            CancellationToken.None);

        Assert.Equal("TEAM BRAVO", Assert.Single(grid.Data).GroupName);
    }

    [Fact]
    public async Task Handle_Search_DoesNotMatchMemberNames()
    {
        // The Auditor Name cell is filled after paging (RM-02 stance) - it is never
        // searched (flagged, the GetAllFindings precedent).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "HIDDEN MEMBER");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader, second], name: "TEAM ALPHA");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery
            {
                Request = new DataGridRequest { SearchTerm = "HIDDEN" }
            },
            CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_ForeignRows_AreExcluded()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklistA = await SeedChecklistAsync(db, CompanyA);
        var checklistB = await SeedChecklistAsync(db, CompanyB);
        var leaderA = await SeedAuditorAsync(db, CompanyA);
        var leaderB = await SeedAuditorAsync(db, CompanyB);
        await SeedGroupAsync(db, CompanyA, checklistA, leaderA, [leaderA], name: "TEAM A");
        await SeedGroupAsync(db, CompanyB, checklistB, leaderB, [leaderB], name: "TEAM B");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery(), CancellationToken.None);

        Assert.Equal("TEAM A", Assert.Single(grid.Data).GroupName);
    }

    [Fact]
    public async Task Handle_GroupWithoutMembers_ReturnsEmptyCell()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [], name: "TEAM SOLO");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(string.Empty, row.AuditorName);
        Assert.Equal("LEAD ONE", row.GroupLeaderName);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyGrid()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsOnlyTheRequestedPage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM A");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM B");
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM C");

        var grid = await Handler(db, user).Handle(
            new GetAllGroupAuditorsQuery
            {
                Request = new DataGridRequest { Page = 2, PageSize = 2 }
            },
            CancellationToken.None);

        Assert.Equal(3, grid.TotalRecords);
        Assert.Single(grid.Data);
        Assert.Equal("TEAM C", grid.Data.Single().GroupName);
    }
}
