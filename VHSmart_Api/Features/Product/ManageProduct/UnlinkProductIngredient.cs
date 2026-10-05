using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The unlink half of the tab's Action icon (spec 9.1): Database.md 9 says link/unlink TOGGLES
// MappingStatus, so the row is kept and marked INACTIVE - never a physical delete, and never a
// soft delete either, so the pair stays on the table showing its status and a later link just
// flips it back. Re-unlinking an already INACTIVE row is a no-op that still answers 204.
// The ingredient id is scoped to the product in the route, so a row of another product answers
// 404 (never 403), and the tab's >= 1 brand rule applies (spec 6.3 / 9.1).
public record UnlinkProductIngredientCommand(Guid ProductId, Guid IngredientId) : IRequest;

public class UnlinkProductIngredientValidator : AbstractValidator<UnlinkProductIngredientCommand>
{
    public UnlinkProductIngredientValidator()
    {
        RuleFor(x => x.IngredientId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Ingredient is required.");
    }
}

public class UnlinkProductIngredientHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UnlinkProductIngredientCommand>
{
    public async Task Handle(UnlinkProductIngredientCommand request, CancellationToken ct)
    {
        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.ProductId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        await ProductIngredientData.EnsureBrandLinkedAsync(db, user, ct);

        var entity = await db.ProductIngredients.FirstOrDefaultAsync(
            row => row.Id == request.IngredientId && row.ProductId == request.ProductId,
            ct);

        if (entity is null)
            throw new NotFoundException("Ingredient not found.");

        entity.MappingStatus = ProductIngredientMappingStatus.Inactive;
        await db.SaveChangesAsync(ct);
    }
}
