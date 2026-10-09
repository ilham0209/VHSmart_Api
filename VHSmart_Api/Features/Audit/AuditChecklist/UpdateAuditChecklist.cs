using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// Step 2 of the two-step save (spec 14.6 [MANUAL]) - and the edit modal's save: the header
// fields plus the Criteria Selection ticked rows arrive together and are stored in one
// save. A checklist that is linked to any planned/performed audit cannot be edited (spec
// 14.6 [MANUAL], Database.md 12 "Locked once used by any AudAuditPlans row - computed"):
// the lock is a state check, so it lives in the handler AFTER the 404 lookup and answers
// 409 with a friendly message (ours - flagged); the validators still run first (the house
// pipeline), so an API caller gets 400 before the lock is ever reached. Unknown or foreign
// row -> 404 (CodingRules 9). The Criteria Selection set is rebuilt - rows no longer ticked
// are soft-deleted, new ticks insert live rows (the AU-03/AU-04 link-rebuild stance; the
// junction's filtered UQ pair frees a re-tick). CompanyId and the row's company are never
// editable (spec 3.3, CodingRules 8.1).
public record UpdateAuditChecklistCommand(
    Guid Id,
    Guid ChecklistCategoryId,
    string Name,
    string? Description,
    IReadOnlyList<Guid>? CriteriaIds) : IRequest<AuditChecklistResponse>;

public class UpdateAuditChecklistValidator : AbstractValidator<UpdateAuditChecklistCommand>
{
    public UpdateAuditChecklistValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.ChecklistCategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Checklist Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == CreateAuditChecklistValidator.ChecklistCategory,
                ct))
            .WithMessage("Checklist Category not found.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Checklist Name is required.")
            .MaximumLength(200)
            .WithMessage("Checklist Name must be 200 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");

        RuleFor(x => x.CriteriaIds)
            .MustAsync(async (ids, ct) =>
            {
                var distinct = (ids ?? Array.Empty<Guid>()).Distinct().ToList();
                if (distinct.Count == 0)
                    return true;
                var found = await db.AuditCriteria.CountAsync(
                    row => distinct.Contains(row.Id) && row.CompanyId == user.CompanyId, ct);
                return found == distinct.Count;
            })
            .WithMessage("Audit Criteria not found.");
    }
}

public class UpdateAuditChecklistHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAuditChecklistCommand, AuditChecklistResponse>
{
    public async Task<AuditChecklistResponse> Handle(
        UpdateAuditChecklistCommand request,
        CancellationToken ct)
    {
        var entity = await db.AuditChecklists.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Audit checklist not found.");

        // Computed lock (Database.md 12): any live AudAuditPlans row that references the
        // checklist freezes it. Only planned/performed audits lock it - a Group Auditor's
        // template pick does not (spec 14.6 scopes the rule to transactions; flagged).
        var locked = await db.AuditPlans.AnyAsync(
            plan => plan.ChecklistId == entity.Id && plan.CompanyId == user.CompanyId, ct);
        if (locked)
            throw new ConflictException(
                "This checklist is used by an audit plan and cannot be edited.");

        entity.ChecklistCategoryId = request.ChecklistCategoryId;
        entity.Name = request.Name;
        entity.Description = request.Description;

        // Rebuild the Criteria Selection: live rows no longer in the payload are un-ticked
        // (soft delete), new ids insert live rows, unchanged links keep their audit columns.
        var desired = (request.CriteriaIds ?? Array.Empty<Guid>()).Distinct().ToHashSet();
        var existingLinks = await db.AuditChecklistCriteria
            .Where(link => link.ChecklistId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in existingLinks.Where(link => !desired.Contains(link.AuditCriteriaId)))
            db.AuditChecklistCriteria.Remove(link);

        var linkedIds = existingLinks
            .Where(link => !link.IsDeleted)
            .Select(link => link.AuditCriteriaId)
            .ToHashSet();
        foreach (var criteriaId in desired.Where(id => !linkedIds.Contains(id)))
        {
            db.AuditChecklistCriteria.Add(new AuditChecklistCriteriaEntity
            {
                CompanyId = user.CompanyId,
                ChecklistId = entity.Id,
                AuditCriteriaId = criteriaId
            });
        }

        await db.SaveChangesAsync(ct);

        return CreateAuditChecklistHandler.ToResponse(entity, [.. desired]);
    }
}
