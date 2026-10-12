using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// Edit a group (spec 14.7 pencil action): the same fields as the add modal. Unknown or
// foreign row -> 404; the Template radio still allows exactly one checklist and the
// leader must still be one of the ticked members (D-10). The member set is rebuilt in one
// save - rows no longer ticked are soft-deleted, new ticks insert live rows, unchanged
// links keep their audit columns (the AU-03/AU-04/AU-05 link-rebuild stance; the
// junction's filtered UQ pair frees a re-tick). NO lock: spec 14.6 freezes checklists once
// used by a plan, but says nothing about groups - a group referenced by a plan edits
// freely (flagged). CompanyId is never editable (spec 3.3, CodingRules 8.1).
public record UpdateGroupAuditorCommand(
    Guid Id,
    string Name,
    GroupAuditorStatus Status,
    Guid ChecklistId,
    Guid LeaderUserId,
    IReadOnlyList<Guid>? MemberIds) : IRequest<GroupAuditorResponse>;

public class UpdateGroupAuditorValidator : AbstractValidator<UpdateGroupAuditorCommand>
{
    public UpdateGroupAuditorValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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
            .MustAsync((id, ct) => CreateGroupAuditorValidator.IsAuditorAsync(
                db, user.CompanyId, id, ct))
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
                    if (await CreateGroupAuditorValidator.IsAuditorAsync(
                            db, user.CompanyId, id, ct))
                        found++;
                }
                return found == distinct.Count;
            })
            .WithMessage("Auditor not found.");

        RuleFor(x => x)
            .Must(command => (command.MemberIds ?? Array.Empty<Guid>())
                .Distinct()
                .Contains(command.LeaderUserId))
            .WithMessage("Group Leader must be one of the members.");
    }
}

public class UpdateGroupAuditorHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateGroupAuditorCommand, GroupAuditorResponse>
{
    public async Task<GroupAuditorResponse> Handle(
        UpdateGroupAuditorCommand request,
        CancellationToken ct)
    {
        var entity = await db.GroupAuditors.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Group auditor not found.");

        entity.Name = request.Name;
        entity.Status = request.Status;
        entity.ChecklistId = request.ChecklistId;
        entity.LeaderUserId = request.LeaderUserId;

        var desired = (request.MemberIds ?? Array.Empty<Guid>()).Distinct().ToHashSet();
        var existingLinks = await db.GroupAuditorMembers
            .Where(link => link.GroupAuditorId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in existingLinks.Where(link => !desired.Contains(link.UserId)))
            db.GroupAuditorMembers.Remove(link);

        var linkedIds = existingLinks
            .Where(link => !link.IsDeleted)
            .Select(link => link.UserId)
            .ToHashSet();
        foreach (var memberId in desired.Where(id => !linkedIds.Contains(id)))
        {
            db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
            {
                CompanyId = user.CompanyId,
                GroupAuditorId = entity.Id,
                UserId = memberId
            });
        }

        await db.SaveChangesAsync(ct);

        return CreateGroupAuditorHandler.ToResponse(entity, [.. desired]);
    }
}
