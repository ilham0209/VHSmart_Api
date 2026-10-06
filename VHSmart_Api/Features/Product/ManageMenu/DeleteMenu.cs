using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// Remove a menu (spec 9.2 list, Action > delete): soft delete (CodingRules 7.1). The
// "List of Company" and "List of Raw Materials" rows stay with the menu, like every other
// child list in this codebase. Sharing is read-only: a menu another company shared with the
// caller answers 404 here (MenuData.EnsureOwner). There is no "delete only when unused" guard
// yet: PrdMenuConceptMenus (PD-05) and the premise "Menu Information" tab (PR-04) reference
// this table - the raw material / product guard stance of RM-02 and PD-01.
public record DeleteMenuCommand(Guid Id) : IRequest;

public class DeleteMenuHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteMenuCommand>
{
    public async Task Handle(DeleteMenuCommand request, CancellationToken ct)
    {
        var entity = await db.Menus
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Menu not found.");

        MenuData.EnsureOwner(entity, user);

        db.Menus.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
