using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Recommendation;

// Edit a recommendation (spec 14.3 pencil action): the same fields as the add modal. Unknown
// or foreign row -> 404; no duplicate rule exists for codes or names (spec 14.3), so any
// values are accepted while the length validators hold. CompanyId and therefore the row's
// company are never editable (spec 3.3, CodingRules 8.1).
public record UpdateRecommendationCommand(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description) : IRequest<UpdateRecommendationResponse>;

public record UpdateRecommendationResponse(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description);

public class UpdateRecommendationValidator : AbstractValidator<UpdateRecommendationCommand>
{
    public UpdateRecommendationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(2000).WithMessage("Name must be 2000 characters or fewer.");

        RuleFor(x => x.RecommendationCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Recommendation Code is required.")
            .MaximumLength(50).WithMessage("Recommendation Code must be 50 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");
    }
}

public class UpdateRecommendationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateRecommendationCommand, UpdateRecommendationResponse>
{
    public async Task<UpdateRecommendationResponse> Handle(
        UpdateRecommendationCommand request,
        CancellationToken ct)
    {
        var entity = await db.Recommendations
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Recommendation not found.");

        entity.Name = request.Name;
        entity.RecommendationCode = request.RecommendationCode;
        entity.Description = request.Description;
        await db.SaveChangesAsync(ct);

        return new UpdateRecommendationResponse(
            entity.Id, entity.Name, entity.RecommendationCode, entity.Description);
    }
}
