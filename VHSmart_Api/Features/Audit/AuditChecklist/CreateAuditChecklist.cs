using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// Step 1 of the two-step save (spec 14.6 [MANUAL]: "save the checklist header first, then
// tick the criteria in Criteria Selection, then save again"): this creates the HEADER ONLY,
// so the command carries no criteria and the response returns an empty selection - the
// ticks are persisted by the subsequent update (flagged). Checklist Category must be a
// live own-company General Data value of Group AUDIT / "Internal - Audit Category" - the
// §14.1 probable mapping ("Manage Audit Checklist > Checklist Category <- an audit-category
// list" [VERIFY], same flag as AU-04). Name is required (spec 14.6 Checklist Name*); no
// name-uniqueness rule exists (Database.md 12 states no UQ - "checklist syariah 2.0" style
// duplicates are ordinary rows). CompanyId comes from the JWT only (CodingRules 8.1).
public record CreateAuditChecklistCommand(
    Guid ChecklistCategoryId,
    string Name,
    string? Description) : IRequest<AuditChecklistResponse>;

// Shared by create, edit and the detail modal - the header fields are the same (14.6).
public record AuditChecklistResponse(
    Guid Id,
    Guid ChecklistCategoryId,
    string Name,
    string? Description,
    IReadOnlyList<Guid> CriteriaIds);

public class CreateAuditChecklistValidator : AbstractValidator<CreateAuditChecklistCommand>
{
    // The probable mapping of spec 14.1 [VERIFY]: Manage Audit Checklist > Checklist
    // Category comes from General Data Group AUDIT, category "Internal - Audit Category".
    public const string ChecklistCategory = "Internal - Audit Category";

    public CreateAuditChecklistValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.ChecklistCategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Checklist Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.AUDIT
                    && row.Category == ChecklistCategory,
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
    }
}

public class CreateAuditChecklistHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateAuditChecklistCommand, AuditChecklistResponse>
{
    public async Task<AuditChecklistResponse> Handle(
        CreateAuditChecklistCommand request,
        CancellationToken ct)
    {
        var entity = new AuditChecklistEntity
        {
            CompanyId = user.CompanyId,
            ChecklistCategoryId = request.ChecklistCategoryId,
            Name = request.Name,
            Description = request.Description
        };
        db.AuditChecklists.Add(entity);
        await db.SaveChangesAsync(ct);

        // Two-step flow: no criteria yet - the selection arrives with the next save.
        return ToResponse(entity, []);
    }

    internal static AuditChecklistResponse ToResponse(
        AuditChecklistEntity entity,
        IReadOnlyList<Guid> criteriaIds) =>
        new(
            entity.Id,
            entity.ChecklistCategoryId,
            entity.Name,
            entity.Description,
            criteriaIds);
}
