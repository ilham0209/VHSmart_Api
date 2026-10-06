using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// "Multiple Delete" of the spec 9.2 toolbar ([CONFIRMED]): one request, one soft delete for
// every selected row. The batch is all-or-nothing - an id that is unknown, foreign or shared
// with the caller answers 404 before anything is deleted, so a stale selection never
// half-applies (the raw material / product bulk delete stance).
public record BulkDeleteMenusCommand(IReadOnlyList<Guid>? Ids) : IRequest;

public class BulkDeleteMenusValidator : AbstractValidator<BulkDeleteMenusCommand>
{
    public BulkDeleteMenusValidator()
    {
        RuleFor(x => x.Ids)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("No rows selected.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("No rows selected.");
    }
}

public class BulkDeleteMenusHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<BulkDeleteMenusCommand>
{
    public async Task Handle(BulkDeleteMenusCommand request, CancellationToken ct)
    {
        var requestedIds = request.Ids!.Distinct().ToList();
        var entities = await db.Menus
            .Where(row => requestedIds.Contains(row.Id))
            .ToListAsync(ct);

        // Missing ids are reported one by one in the same order the caller sent them, so the
        // message points at the row that actually failed.
        foreach (var id in requestedIds)
        {
            var entity = entities.FirstOrDefault(row => row.Id == id);
            if (entity is null)
                throw new NotFoundException("Menu not found.");

            MenuData.EnsureOwner(entity, user);
        }

        db.Menus.RemoveRange(entities);
        await db.SaveChangesAsync(ct);
    }
}
