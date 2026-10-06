using FluentValidation;
using MediatR;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenuConcept;

// Add a concept (spec 9.3 modal "Manage Menu Concept"): Menu Concept* and Description, with
// "For Company*" being the caller's own company from the JWT - the field is a display of who
// owns the row, so it is never read from the body (CodingRules 8.1). The concept is saved
// FIRST and its "List of Menu" is saved afterwards through the update (spec 9.3: "save; then
// List of Menu ... then Save"), so a create writes no link rows at all.
// No duplicate-name rule: Database.md 9 defines no unique index for PrdMenuConcepts and the
// legacy "concept-name check" states no scope - flagged for the owner instead of invented.
public record CreateMenuConceptCommand(string? Name, string? Description)
    : IRequest<MenuConceptResponse>;

public class CreateMenuConceptValidator : AbstractValidator<CreateMenuConceptCommand>
{
    public CreateMenuConceptValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Menu concept name is required.")
            .MaximumLength(200).WithMessage("Menu concept name must be 200 characters or fewer.");

        RuleFor(x => x.Description).MaximumLength(1000)
            .WithMessage("Description must be 1000 characters or fewer.");
    }
}

public class CreateMenuConceptHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateMenuConceptCommand, MenuConceptResponse>
{
    public async Task<MenuConceptResponse> Handle(
        CreateMenuConceptCommand request,
        CancellationToken ct)
    {
        var entity = MenuConceptData.Apply(new MenuConceptEntity
        {
            CompanyId = user.CompanyId
        }, request);

        db.MenuConcepts.Add(entity);
        await db.SaveChangesAsync(ct);

        return await MenuConceptResponseData.From(db, entity, ct);
    }
}
