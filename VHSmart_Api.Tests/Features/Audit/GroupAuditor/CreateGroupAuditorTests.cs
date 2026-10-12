using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using static VHSmart_Api.Tests.Features.Audit.GroupAuditor.GroupAuditorTestData;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class CreateGroupAuditorTests
{
    private static CreateGroupAuditorHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesGroupAndMembersForTheJwtCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");

        var response = await Handler(db, user).Handle(
            new CreateGroupAuditorCommand(
                "TEAM ALPHA",
                GroupAuditorStatus.Active,
                checklist,
                leader,
                [leader, second]),
            CancellationToken.None);

        var stored = await db.GroupAuditors.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("TEAM ALPHA", stored.Name);
        Assert.Equal(GroupAuditorStatus.Active, stored.Status);
        Assert.Equal(checklist, stored.ChecklistId);
        Assert.Equal(leader, stored.LeaderUserId);
        Assert.Equal(stored.Id, response.Id);

        var members = await db.GroupAuditorMembers.AsNoTracking()
            .Where(row => row.GroupAuditorId == stored.Id)
            .Select(row => row.UserId)
            .ToListAsync();
        Assert.Equal(2, members.Count);
        Assert.Contains(leader, members);
        Assert.Contains(second, members);
    }

    [Fact]
    public async Task Handle_InactiveStatus_PersistsTheSpecEnum()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);

        await Handler(db, user).Handle(
            new CreateGroupAuditorCommand(
                "TEAM IDLE",
                GroupAuditorStatus.Inactive,
                checklist,
                leader,
                [leader]),
            CancellationToken.None);

        var stored = await db.GroupAuditors.SingleAsync();
        Assert.Equal(GroupAuditorStatus.Inactive, stored.Status);
        // CodingRules 11: the column stores the spec string.
        Assert.Equal("Inactive", stored.Status.ToString());
    }

    [Fact]
    public async Task Handle_DuplicateMemberIds_SavesTheMemberOnce()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateGroupAuditorCommand(
                "TEAM ALPHA",
                GroupAuditorStatus.Active,
                checklist,
                leader,
                [leader, leader]),
            CancellationToken.None);

        var members = await db.GroupAuditorMembers
            .Where(row => row.GroupAuditorId == response.Id)
            .ToListAsync();
        Assert.Single(members);
        Assert.Equal([leader], response.MemberIds);
    }

    [Fact]
    public async Task Handle_DuplicateGroupName_IsAllowed()
    {
        // Database.md 13 states NO unique on Name - duplicate group names are ordinary rows.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        await SeedGroupAsync(db, CompanyA, checklist, leader, [leader], name: "TEAM ALPHA");

        var response = await Handler(db, user).Handle(
            new CreateGroupAuditorCommand(
                "TEAM ALPHA",
                GroupAuditorStatus.Active,
                checklist,
                leader,
                [leader]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.GroupAuditors.CountAsync());
    }

    [Fact]
    public async Task Handle_DeactivatedAuditor_Accepted()
    {
        // D-10 defines an auditor by role only - it states no IsActive condition (flagged).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, isActive: false);

        var response = await Handler(db, user).Handle(
            new CreateGroupAuditorCommand(
                "TEAM ALPHA",
                GroupAuditorStatus.Active,
                checklist,
                leader,
                [leader]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand("", GroupAuditorStatus.Active, Guid.NewGuid(), Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Name is required.");
    }

    [Fact]
    public async Task Validator_NameOver200_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                new string('x', 201), GroupAuditorStatus.Active, Guid.NewGuid(), Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_EmptyChecklist_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand("TEAM", GroupAuditorStatus.Active, Guid.Empty, Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Template is required.");
    }

    [Fact]
    public async Task Validator_ForeignChecklist_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignChecklist = await SeedChecklistAsync(db, CompanyB);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, foreignChecklist, Guid.NewGuid(), []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Template not found.");
    }

    [Fact]
    public async Task Validator_EmptyLeader_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, checklist, Guid.Empty, []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Leader is required.");
    }

    [Fact]
    public async Task Validator_LeaderNotAuditorRole_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var admin = await SeedNonAuditorAsync(db, CompanyA);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, checklist, admin, [admin]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Auditor not found.");
    }

    [Fact]
    public async Task Validator_MemberWithoutMembership_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var foreignAuditor = await SeedAuditorAsync(db, CompanyB);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader, foreignAuditor]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Auditor not found.");
    }

    [Fact]
    public async Task Validator_LeaderNotAmongMembers_Fails()
    {
        // D-10: "Group leader must be one of the members."
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var second = await SeedAuditorAsync(db, CompanyA, name: "AUDITOR TWO");
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, checklist, leader, [second]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Leader must be one of the members.");
    }

    [Fact]
    public async Task Validator_EmptyMembersWithLeader_FailsTheLeaderRule()
    {
        // The spec derives the "at least one auditor" need from the leader rule (flagged).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var validator = new CreateGroupAuditorValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateGroupAuditorCommand(
                "TEAM", GroupAuditorStatus.Active, checklist, leader, []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Leader must be one of the members.");
    }
}
