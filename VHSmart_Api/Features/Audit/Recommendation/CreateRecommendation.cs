using FluentValidation;
using MediatR;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Recommendation;

// Add a recommendation (spec 14.3 add modal "Add Recommendation": Name* text area,
// Recommendation Code* text, Description - both buttons seen). Name and Recommendation Code
// are mandatory and capped at the Database.md 14.3 lengths (2000 / 50 / 1000); there is NO
// duplicate rule - codes are free text (spec 14.3 [CONFIRMED], Database.md 14.3 "Free-text
// code"). CompanyId comes from the JWT only (CodingRules 8.1).
public record CreateRecommendationCommand(
    string Name,
    string RecommendationCode,
    string? Description) : IRequest<CreateRecommendationResponse>;

public record CreateRecommendationResponse(
    Guid Id,
    string Name,
    string RecommendationCode,
    string? Description);

public class CreateRecommendationValidator : AbstractValidator<CreateRecommendationCommand>
{
    public CreateRecommendationValidator()
    {
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

public class CreateRecommendationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateRecommendationCommand, CreateRecommendationResponse>
{
    public async Task<CreateRecommendationResponse> Handle(
        CreateRecommendationCommand request,
        CancellationToken ct)
    {
        var entity = new RecommendationEntity
        {
            CompanyId = user.CompanyId,
            Name = request.Name,
            RecommendationCode = request.RecommendationCode,
            Description = request.Description
        };
        db.Recommendations.Add(entity);
        await db.SaveChangesAsync(ct);

        return new CreateRecommendationResponse(
            entity.Id, entity.Name, entity.RecommendationCode, entity.Description);
    }
}
