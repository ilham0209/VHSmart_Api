using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// "Multiple Delete" of the spec 9.1 toolbar ([CONFIRMED]): one request, one soft delete for
// every selected row. The batch is all-or-nothing - an id that is unknown or foreign answers
// 404 before anything is deleted, so a stale selection never half-applies (same stance as
// the raw material bulk delete).
public record BulkDeleteProductsCommand(IReadOnlyList<Guid>? Ids) : IRequest;

public class BulkDeleteProductsValidator : AbstractValidator<BulkDeleteProductsCommand>
{
    public BulkDeleteProductsValidator()
    {
        RuleFor(x => x.Ids)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("No rows selected.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("No rows selected.");
    }
}

public class BulkDeleteProductsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<BulkDeleteProductsCommand>
{
    public async Task Handle(BulkDeleteProductsCommand request, CancellationToken ct)
    {
        var requestedIds = request.Ids!.Distinct().ToList();
        var entities = await db.Products
            .Where(row => requestedIds.Contains(row.Id)
                && row.CompanyId == user.CompanyId)
            .ToListAsync(ct);

        // Missing ids are reported one by one in the same order the caller sent them, so the
        // message points at the row that actually failed.
        foreach (var id in requestedIds)
        {
            var entity = entities.FirstOrDefault(row => row.Id == id);
            if (entity is null)
                throw new NotFoundException("Product not found.");
        }

        // The same in-use guard as the row delete (the toolbar must not free a product the
        // row delete refuses); the check runs before anything is removed, so the batch stays
        // all-or-nothing.
        foreach (var entity in entities)
            await ProductDeletionGuard.EnsureNotInUseAsync(db, entity.Id, ct);

        db.Products.RemoveRange(entities);
        await db.SaveChangesAsync(ct);
    }
}
