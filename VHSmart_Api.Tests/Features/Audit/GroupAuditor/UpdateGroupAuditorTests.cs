using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class UpdateGroupAuditorTests
{
    private static UpdateGroupAuditorHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesHeaderAndReplacesMembers()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var otherChecklist = await SeedChecklistAsync(db, CompanyA, name: "Checklist B");
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");
        var third = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR THREE");
        var groupId = await SeedGroupAsync(
            db, CompanyA, checklist, leader, [leader, second], name: "TEAM OLD");

        var response = await Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                groupId,
                "TEAM NEW",
                GroupAuditorStatus.Inactive,
                otherChecklist,
                third,
                [third, leader]),
            CancellationToken.None);

        var stored = await db.GroupAuditors.SingleAsync(row => row.Id == groupId);
        Assert.Equal("TEAM NEW", stored.Name);
        Assert.Equal(GroupAuditorStatus.Inactive, stored.Status);
        Assert.Equal(otherChecklist, stored.ChecklistId);
        Assert.Equal(third, stored.LeaderUserId);
        Assert.Equal(CompanyA, stored.CompanyId);

        var members = await db.GroupAuditorMembers.AsNoTracking()
            .Where(row => row.GroupAuditorId == groupId && !row.IsDeleted)
            .Select(row => row.UserId)
            .ToListAsync();
        Assert.Equal(2, members.Count);
        Assert.Contains(third, members);
        Assert.Contains(leader, members);
        Assert.DoesNotContain(second, members);

        Assert.Equal(response.MemberIds.OrderBy(id => id), members.OrderBy(id => id));
    }

    [Fact]
    public async Task Handle_UnlinkedMember_IsSoftDeleted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var second = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader, second]);
        var secondLink = await db.GroupAuditorMembers
            .SingleAsync(row => row.GroupAuditorId == groupId && row.UserId == second);

        await Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                groupId, "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader]),
            CancellationToken.None);

        var link = await db.GroupAuditorMembers.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == secondLink.Id);
        Assert.True(link.IsDeleted);
    }

    [Fact]
    public async Task Handle_ReTickFreedMember_CreatesALiveRowAgain()
    {
        // The junction's filtered UQ pair frees a re-tick (AU-03/AU-05 stance).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var second = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader, second]);

        await Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                groupId, "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader]),
            CancellationToken.None);
        await Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                groupId, "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader, second]),
            CancellationToken.None);

        var live = await db.GroupAuditorMembers
            .Where(row => row.GroupAuditorId == groupId && row.UserId == second && !row.IsDeleted)
            .ToListAsync();
        Assert.Single(live);
    }

    [Fact]
    public async Task Handle_UnchangedMembers_KeepTheirRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var groupId = await SeedGroupAsync(db, CompanyA, checklist, leader, [leader]);
        var memberRow = await db.GroupAuditorMembers
            .SingleAsync(row => row.GroupAuditorId == groupId && row.UserId == leader);

        await Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                groupId, "TEAM RENAMED", GroupAuditorStatus.Active, checklist, leader, [leader]),
            CancellationToken.None);

        var after = await db.GroupAuditorMembers.SingleAsync(row => row.Id == memberRow.Id);
        Assert.False(after.IsDeleted);
        Assert.Equal(memberRow.SysUserCreated, after.SysUserCreated);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        var foreignId = await SeedGroupAsync(db, CompanyB, checklist, leader, [leader]);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler(db, user).Handle(
            new UpdateGroupAuditorCommand(
                foreignId, "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new UpdateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateGroupAuditorCommand(
                Guid.NewGuid(), "", GroupAuditorStatus.Active, Guid.NewGuid(), Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Name is required.");
    }

    [Fact]
    public async Task Validator_ForeignChecklist_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignChecklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var validator = new UpdateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateGroupAuditorCommand(
                Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, foreignChecklist, leader, [leader]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Template not found.");
    }

    [Fact]
    public async Task Validator_LeaderNotAuditorRole_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var admin = await SeedNonAuditorAsync(db, CompanyA);
        var validator = new UpdateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateGroupAuditorCommand(
                Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, checklist, admin, [admin]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Auditor not found.");
    }

    [Fact]
    public async Task Validator_LeaderNotAmongMembers_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");
        var validator = new UpdateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateGroupAuditorCommand(
                Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, checklist, leader, [second]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Leader must be one of the members.");
    }
}
