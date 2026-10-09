using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Finding;

// Edit a finding (spec 14.4 pencil action): the same fields as the add modal, including
// the Recommendation* checkbox table. Unknown or foreign row -> 404; the code unique rule
// (Database.md 11) excludes the row itself so a free code can be taken (the UpdateBatch
// stance); the link set is rebuilt - rows no longer checked are soft-deleted (unlinking
// frees the UQ pair) and new checks insert live rows, all in one save. CompanyId and
// therefore the row's company are never editable (spec 3.3, CodingRules 8.1).
public record UpdateFindingCommand(
    Guid Id,
    string Name,
    string FindingCode,
    string? Description,
    IReadOnlyList<Guid> Recommendations) : IRequest<FindingResponse>;

public class UpdateFindingValidator : AbstractValidator<UpdateFindingCommand>
{
    public UpdateFindingValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id).NotEmpty();

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

public class UpdateFindingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateFindingCommand, FindingResponse>
{
    public async Task<FindingResponse> Handle(
        UpdateFindingCommand request,
        CancellationToken ct)
    {
        var entity = await db.Findings
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Finding not found.");

        var duplicate = await db.Findings.AnyAsync(
            row => row.CompanyId == user.CompanyId
                && row.FindingCode == request.FindingCode
                && row.Id != request.Id,
            ct);
        if (duplicate)
            throw new ConflictException("A finding with this code already exists.");

        entity.Name = request.Name;
        entity.FindingCode = request.FindingCode;
        entity.Description = request.Description;

        // Rebuild the link set: live rows no longer in the payload are un-linked (soft
        // delete - Database.md 1 conventions), new ids insert live rows. Rows already
        // linked stay untouched so the audit columns of an unchanged link are preserved.
        var recommendationIds = request.Recommendations.Distinct().ToHashSet();
        var existingLinks = await db.FindingRecommendations
            .Where(link => link.FindingId == entity.Id)
            .ToListAsync(ct);

        foreach (var link in existingLinks.Where(link => !recommendationIds.Contains(link.RecommendationId)))
            db.FindingRecommendations.Remove(link);

        var linkedIds = existingLinks
            .Where(link => !link.IsDeleted)
            .Select(link => link.RecommendationId)
            .ToHashSet();
        foreach (var recommendationId in recommendationIds.Where(id => !linkedIds.Contains(id)))
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
            entity.Id, entity.Name, entity.FindingCode, entity.Description,
            [.. recommendationIds]);
    }
}
