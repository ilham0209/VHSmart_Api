using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// Add a criteria row (spec 14.5 add modal: Category* dropdown, Category Sequence* default 0,
// Criteria* searchable dropdown + inline "+", Criteria Sequence* default 0, Sub Criteria
// dropdown + "+", Reference Category, Reference, Potential Point default 1, Description,
// Finding Selection dropdown - [CONFIRMED / MANUAL]). Category must be a live own-company
// General Data value of Group AUDIT / "Internal - Audit Category" - the 14.1 probable
// mapping ("Manage Audit Criteria > Category ... an audit-category list"), read against the
// internal-audit ownership of checklists and D-10's use of the External pair for the report
// screens [VERIFY - question 36, flagged]. Criteria / Sub Criteria must be live own-company
// master rows of the matching Kind; Findings are optional (link optional, spec 14.5
// [MANUAL]) and every id must be a live own-company finding. Spec 14.5 shows criteria text
// repeating across rows, so there is NO duplicate rule on the row itself; the master UQ
// (CompanyId, Kind, Text) is enforced on the inline "+" create instead. CompanyId comes from
// the JWT only (CodingRules 8.1).
public record CreateAuditCriteriaCommand(
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

// Shared by create, edit and the detail modal - the modal fields are the same (spec 14.5).
public record AuditCriteriaResponse(
    Guid Id,
    Guid CategoryId,
    int CategorySequence,
    Guid CriteriaId,
    int CriteriaSequence,
    Guid? SubCriteriaId,
    string? ReferenceCategory,
    string? Reference,
    decimal PotentialPoint,
    string? Description,
    IReadOnlyList<Guid> Findings);

public class CreateAuditCriteriaValidator : AbstractValidator<CreateAuditCriteriaCommand>
{
    // The probable mapping of spec 14.1 [VERIFY]: Manage Audit Criteria > Category comes
    // from General Data Group AUDIT, category "Internal - Audit Category".
    public const string AuditCategory = "Internal - Audit Category";

    public CreateAuditCriteriaValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == AuditCategory,
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

public class CreateAuditCriteriaHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateAuditCriteriaCommand, AuditCriteriaResponse>
{
    public async Task<AuditCriteriaResponse> Handle(
        CreateAuditCriteriaCommand request,
        CancellationToken ct)
    {
        var entity = new AuditCriteriaEntity
        {
            CompanyId = user.CompanyId,
            CategoryId = request.CategoryId,
            CategorySequence = request.CategorySequence,
            CriteriaId = request.CriteriaId,
            CriteriaSequence = request.CriteriaSequence,
            SubCriteriaId = request.SubCriteriaId is Guid sc && sc != Guid.Empty ? sc : null,
            ReferenceCategory = request.ReferenceCategory,
            Reference = request.Reference,
            // Database.md 11 default 1 when the modal sends nothing (v4.9.2 shows 1).
            PotentialPoint = request.PotentialPoint ?? 1m,
            Description = request.Description
        };
        db.AuditCriteria.Add(entity);

        // Finding Selection saved with the row (one save); duplicate payload ids collapse
        // - the junction carries no DB unique (Database.md 11 states none for this table).
        var findingIds = (request.Findings ?? Array.Empty<Guid>()).Distinct().ToList();
        foreach (var findingId in findingIds)
        {
            db.AuditCriteriaFindings.Add(new AuditCriteriaFindingEntity
            {
                CompanyId = user.CompanyId,
                AuditCriteriaId = entity.Id,
                FindingId = findingId
            });
        }

        await db.SaveChangesAsync(ct);

        return ToResponse(entity, findingIds);
    }

    internal static AuditCriteriaResponse ToResponse(
        AuditCriteriaEntity entity,
        IReadOnlyList<Guid> findingIds) =>
        new(
            entity.Id,
            entity.CategoryId,
            entity.CategorySequence,
            entity.CriteriaId,
            entity.CriteriaSequence,
            entity.SubCriteriaId,
            entity.ReferenceCategory,
            entity.Reference,
            entity.PotentialPoint,
            entity.Description,
            findingIds);
}
