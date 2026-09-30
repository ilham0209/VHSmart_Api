using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.WebLinks;

// Remove a Web Link (spec 5.4 list: delete action): soft delete (CodingRules 7.1) - the icon
// bytes stay with the row, exactly like the CB logo, so a soft-deleted row keeps a consistent
// File column group. The tenant filter makes another company's row (or an already deleted one)
// answer 404 instead of 204 (CodingRules 9).
public record DeleteWebLinkCommand(Guid Id) : IRequest;

public class DeleteWebLinkHandler(VHSmartDbContext db)
    : IRequestHandler<DeleteWebLinkCommand>
{
    public async Task Handle(DeleteWebLinkCommand request, CancellationToken ct)
    {
        var entity = await db.WebLinks
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Web link not found.");

        db.WebLinks.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
