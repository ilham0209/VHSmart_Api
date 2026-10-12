using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// Add a group (spec 14.7 modal [CONFIRMED / MANUAL]): Group Name*, Group Status*
// (Active/Inactive), the Template Selection radio - exactly one checklist per group
// (Database.md 13 ChecklistId required) - and the auditor table: tick members, then tick
// the Group Leader AMONG the ticked members (D-10 "Group leader must be one of the
// members"; spec 14.7 [MANUAL]). An auditor is an AdmUsers row with role Auditor / Chief
// Auditor (the seeded "Auditor / Chief Auditor" role, D-10 - resolves the spec 14.7
// [VERIFY] "source list not confirmed") that belongs to the caller's company via
// AdmUserCompanies; the role membership check runs in the validator so a foreign or wrong-
// role id is refused before the handler (flagged: D-10 states no IsActive condition, so
// deactivated auditors remain selectable). Members and leader are saved with the group in
// one save; CompanyId comes from the JWT only (CodingRules 8.1).
public record CreateGroupAuditorCommand(
    string Name,
    GroupAuditorStatus Status,
    Guid ChecklistId,
    Guid LeaderUserId,
    IReadOnlyList<Guid>? MemberIds) : IRequest<GroupAuditorResponse>;

// Shared by create, edit and the detail modal - the modal fields are the same (14.7).
public record GroupAuditorResponse(
    Guid Id,
    string Name,
    GroupAuditorStatus Status,
    Guid ChecklistId,
    Guid LeaderUserId,
    IReadOnlyList<Guid> MemberIds);

public class CreateGroupAuditorValidator : AbstractValidator<CreateGroupAuditorCommand>
{
    public CreateGroupAuditorValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Group Name is required.")
            .MaximumLength(200)
            .WithMessage("Group Name must be 200 characters or fewer.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Group Status must be Active or Inactive.");

        RuleFor(x => x.ChecklistId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Template is required.")
            .MustAsync((id, ct) => db.AuditChecklists.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Template not found.");

        RuleFor(x => x.LeaderUserId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Group Leader is required.")
            .MustAsync((id, ct) => IsAuditorAsync(db, user.CompanyId, id, ct))
            .WithMessage("Auditor not found.");

        RuleFor(x => x.MemberIds)
            .MustAsync(async (ids, ct) =>
            {
                var distinct = (ids ?? Array.Empty<Guid>()).Distinct().ToList();
                if (distinct.Count == 0)
                    return true;
                var found = 0;
                foreach (var id in distinct)
                {
                    if (await IsAuditorAsync(db, user.CompanyId, id, ct))
                        found++;
                }
                return found == distinct.Count;
            })
            .WithMessage("Auditor not found.");

        // D-10: the leader must be one of the ticked members. An empty member list fails
        // here too (the spec derives the "at least one auditor" need from the leader rule
        // - flagged).
        RuleFor(x => x)
            .Must(command => (command.MemberIds ?? Array.Empty<Guid>())
                .Distinct()
                .Contains(command.LeaderUserId))
            .WithMessage("Group Leader must be one of the members.");
    }

    // D-10: an AdmUsers row with role Auditor / Chief Auditor in the caller's company.
    internal static Task<bool> IsAuditorAsync(
        VHSmartDbContext db,
        Guid companyId,
        Guid userId,
        CancellationToken ct) =>
        db.Users.AnyAsync(
            user => user.Id == userId
                && user.RoleId == RoleSeedData.AuditorRoleId
                && db.UserCompanies.Any(membership =>
                    membership.UserId == user.Id && membership.CompanyId == companyId),
            ct);
}

public class CreateGroupAuditorHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateGroupAuditorCommand, GroupAuditorResponse>
{
    public async Task<GroupAuditorResponse> Handle(
        CreateGroupAuditorCommand request,
        CancellationToken ct)
    {
        // NO name-uniqueness rule: Database.md 13 states no UQ on Name.
        var entity = new GroupAuditorEntity
        {
            CompanyId = user.CompanyId,
            Name = request.Name,
            Status = request.Status,
            ChecklistId = request.ChecklistId,
            LeaderUserId = request.LeaderUserId
        };
        db.GroupAuditors.Add(entity);

        var memberIds = (request.MemberIds ?? Array.Empty<Guid>()).Distinct().ToList();
        foreach (var memberId in memberIds)
        {
            db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
            {
                CompanyId = user.CompanyId,
                GroupAuditorId = entity.Id,
                UserId = memberId
            });
        }

        await db.SaveChangesAsync(ct);

        return ToResponse(entity, memberIds);
    }

    internal static GroupAuditorResponse ToResponse(
        GroupAuditorEntity entity,
        IReadOnlyList<Guid> memberIds) =>
        new(
            entity.Id,
            entity.Name,
            entity.Status,
            entity.ChecklistId,
            entity.LeaderUserId,
            memberIds);
}
