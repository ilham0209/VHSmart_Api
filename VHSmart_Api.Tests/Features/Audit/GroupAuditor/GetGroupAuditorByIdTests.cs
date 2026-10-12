using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class GetGroupAuditorByIdTests
{
    private static GetGroupAuditorByIdHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidId_ReturnsHeaderLeaderAndMemberIds()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");
        var groupId = await SeedGroupAsync(
            db, CompanyA, checklist, leader, [leader, second],
            name: "TEAM ALPHA", status: GroupAuditorStatus.Inactive);

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorByIdQuery(groupId), CancellationToken.None);

        Assert.Equal(groupId, response.Id);
        Assert.Equal("TEAM ALPHA", response.Name);
        Assert.Equal(GroupAuditorStatus.Inactive, response.Status);
        Assert.Equal(checklist, response.ChecklistId);
        Assert.Equal(leader, response.LeaderUserId);
        Assert.Equal(2, response.MemberIds.Count);
        Assert.Contains(leader, response.MemberIds);
        Assert.Contains(second, response.MemberIds);
    }

    [Fact]
    public async Task Handle_SoftDeletedMembers_AreExcluded()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var second = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader, second]);
        var secondLink = await db.GroupAuditorMembers
            .SingleAsync(row => row.GroupAuditorId == groupId && row.UserId == second);
        db.GroupAuditorMembers.Remove(secondLink);
        await db.SaveChangesAsync();

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorByIdQuery(groupId), CancellationToken.None);

        Assert.Equal([leader], response.MemberIds);
    }

    [Fact]
    public async Task Handle_GroupWithoutMembers_ReturnsEmptyList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, []);

        var response = await Handler(db, user).Handle(
            new GetGroupAuditorByIdQuery(groupId), CancellationToken.None);

        Assert.Empty(response.MemberIds);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handler(db, user).Handle(new GetGroupAuditorByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        var foreignId = await SeedGroupAsync(db, CompanyB, checklist, leader, [leader]);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Handler(db, user).Handle(new GetGroupAuditorByIdQuery(foreignId), CancellationToken.None));
    }
}
