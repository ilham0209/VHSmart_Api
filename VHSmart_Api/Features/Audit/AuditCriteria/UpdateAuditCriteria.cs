using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// Edit a criteria row (spec 14.5 pencil action): the same fields as the add modal. Unknown
// or foreign row -> 404; no duplicate rule on the row itself (the spec's own sample repeats
// the same criteria text across rows); the Finding Selection set is rebuilt - rows no
// longer checked are soft-deleted, new checks insert live rows, all in one save (the
// AU-03 link-rebuild stance). CompanyId and therefore the row's company are never editable
// (spec 3.3, CodingRules 8.1).
public record UpdateAuditCriteriaCommand(
    Guid Id,
    Guid CategoryId,
    int CategorySequence,
    Guid CriteriaId,
    int CriteriaSequence,
    Guid? SubCriteriaId,
    string? ReferenceCategory,
    string? Reference,
    decimal? PotentialPoint,
    string? Description,
    IReadOnlyList<Guid>? Findings) : IRequest<AuditCriteriaResponse>;

public class UpdateAuditCriteriaValidator : AbstractValidator<UpdateAuditCriteriaCommand>
{
    public UpdateAuditCriteriaValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == CreateAuditCriteriaValidator.AuditCategory,
                ct))
            .WithMessage("Category not found.");

        RuleFor(x => x.CriteriaId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Criteria is required.")
            .MustAsync((id, ct) => db.AuditCriteriaMasters.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Kind == AuditCriteriaMasterKind.Criteria,
                ct))
            .WithMessage("Criteria not found.");

        RuleFor(x => x.SubCriteriaId)
            .MustAsync(async (id, ct) => !id.HasValue || id == Guid.Empty
                || await db.AuditCriteriaMasters.AnyAsync(
                    row => row.Id == id.Value
                        && row.CompanyId == user.CompanyId
                        && row.Kind == AuditCriteriaMasterKind.SubCriteria,
                    ct))
            .WithMessage("Sub Criteria not found.");

        RuleFor(x => x.ReferenceCategory)
            .MaximumLength(100)
            .WithMessage("Reference Category must be 100 characters or fewer.");

        RuleFor(x => x.Reference)
            .MaximumLength(200)
            .WithMessage("Reference must be 200 characters or fewer.");

        RuleFor(x => x.PotentialPoint)
            .Must(value => value is null || value >= 0)
            .WithMessage("Potential Point must be 0 or more.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");

        RuleFor(x => x.Findings)
            .MustAsync(async (ids, ct) =>
            {
                var distinct = (ids ?? Array.Empty<Guid>()).Distinct().ToList();
                if (distinct.Count == 0)
                    return true;
                var found = await db.Findings.CountAsync(
                    row => distinct.Contains(row.Id) && row.CompanyId == user.CompanyId, ct);
                return found == distinct.Count;
            })
            .WithMessage("Finding not found.");
    }
}

public class UpdateAuditCriteriaHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAuditCriteriaCommand, AuditCriteriaResponse>
{
    public async Task<AuditCriteriaResponse> Handle(
        UpdateAuditCriteriaCommand request,
        CancellationToken ct)
    {
        var entity = await db.AuditCriteria
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Audit criteria not found.");

        entity.CategoryId = request.CategoryId;
        entity.CategorySequence = request.CategorySequence;
        entity.CriteriaId = request.CriteriaId;
        entity.CriteriaSequence = request.CriteriaSequence;
        entity.SubCriteriaId = request.SubCriteriaId is Guid sc && sc != Guid.Empty ? sc : null;
        entity.ReferenceCategory = request.ReferenceCategory;
        entity.Reference = request.Reference;
        entity.PotentialPoint = request.PotentialPoint ?? 1m;
        entity.Description = request.Description;

        // Rebuild the Finding Selection set: live rows no longer in the payload are
        // un-linked (soft delete), new ids insert live rows, unchanged links keep their
        // audit columns (the AU-03 stance).
        var desired = (request.Findings ?? Array.Empty<Guid>()).Distinct().ToHashSet();
        var existingLinks = await db.AuditCriteriaFindings
            .Where(link => link.AuditCriteriaId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in existingLinks.Where(link => !desired.Contains(link.FindingId)))
            db.AuditCriteriaFindings.Remove(link);

        var linkedIds = existingLinks
            .Where(link => !link.IsDeleted)
            .Select(link => link.FindingId)
            .ToHashSet();
        foreach (var findingId in desired.Where(id => !linkedIds.Contains(id)))
        {
            db.AuditCriteriaFindings.Add(new AuditCriteriaFindingEntity
            {
                CompanyId = user.CompanyId,
                AuditCriteriaId = entity.Id,
                FindingId = findingId
            });
        }

        await db.SaveChangesAsync(ct);

        return CreateAuditCriteriaHandler.ToResponse(entity, [.. desired]);
    }
}
