using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageMenu;

// Remove a menu (spec 9.2 list, Action > delete): soft delete (CodingRules 7.1). The
// "List of Company" and "List of Raw Materials" rows stay with the menu, like every other
// child list in this codebase. Sharing is read-only: a menu another company shared with the
// caller answers 404 here (MenuData.EnsureOwner). There is no "delete only when unused" guard:
// PrdMenuConceptMenus (PD-05) references this table, and PD-05 chose the RM-02 / PD-01 stance
// - the link rows stay behind and every join through them reads !IsDeleted by hand
// (MenuConceptData.LoadMenuInfoAsync), so a menu deleted after it was linked simply drops out
// of the concept's "List of Menu". The premise "Menu Information" tab (PR-04) is still open.
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
