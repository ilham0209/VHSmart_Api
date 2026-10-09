using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Finding;

// Add a finding (spec 14.4 add modal "Add Finding": Name*, Finding Code*, Description, and
// the required "Recommendation*" checkbox table). Name and Finding Code are mandatory and
// capped at the Database.md 11 lengths (2000 / 100 / 1000); at least one Recommendation is
// required [CONFIRMED / MANUAL] and every id must be a live row of the caller's own company
// (the checkbox table serves exactly that set). The unique rule (Database.md 11 UQ
// (CompanyId, FindingCode) non-deleted) means the code is taken while a live row exists:
// 409 with a friendly message instead of letting the index fire. Spec 21.9 also claims the
// description is unique, but Database.md carries no such index - not enforced (flagged).
// CompanyId comes from the JWT only (CodingRules 8.1).
public record CreateFindingCommand(
    string Name,
    string FindingCode,
    string? Description,
    IReadOnlyList<Guid> Recommendations) : IRequest<FindingResponse>;

// Shared by create and edit - the modal fields are the same (spec 14.4).
public record FindingResponse(
    Guid Id,
    string Name,
    string FindingCode,
    string? Description,
    IReadOnlyList<Guid> Recommendations);

public class CreateFindingValidator : AbstractValidator<CreateFindingCommand>
{
    public CreateFindingValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(2000).WithMessage("Name must be 2000 characters or fewer.");

        RuleFor(x => x.FindingCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Finding Code is required.")
            .MaximumLength(100).WithMessage("Finding Code must be 100 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");

        RuleFor(x => x.Recommendations)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("At least one recommendation is required.")
            .MustAsync(async (ids, ct) =>
            {
                var distinct = ids.Distinct().ToList();
                var found = await db.Recommendations.CountAsync(
                    row => distinct.Contains(row.Id) && row.CompanyId == user.CompanyId, ct);
                return found == distinct.Count;
            })
            .WithMessage("Recommendation not found.");
    }
}

public class CreateFindingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateFindingCommand, FindingResponse>
{
    public async Task<FindingResponse> Handle(
        CreateFindingCommand request,
        CancellationToken ct)
    {
        var duplicate = await db.Findings.AnyAsync(
            row => row.CompanyId == user.CompanyId && row.FindingCode == request.FindingCode, ct);
        if (duplicate)
            throw new ConflictException("A finding with this code already exists.");

        var entity = new FindingEntity
        {
            CompanyId = user.CompanyId,
            Name = request.Name,
            FindingCode = request.FindingCode,
            Description = request.Description
        };
        db.Findings.Add(entity);

        // The checkbox table saved with the header (one save): duplicates collapse so the
        // filtered UQ pair never fires on a repeated id in the payload.
        var recommendationIds = request.Recommendations.Distinct().ToList();
        foreach (var recommendationId in recommendationIds)
        {
            db.FindingRecommendations.Add(new FindingRecommendationEntity
            {
                CompanyId = user.CompanyId,
                FindingId = entity.Id,
                RecommendationId = recommendationId
            });
        }

        await db.SaveChangesAsync(ct);

        return new FindingResponse(
            entity.Id, entity.Name, entity.FindingCode, entity.Description, recommendationIds);
    }
}
