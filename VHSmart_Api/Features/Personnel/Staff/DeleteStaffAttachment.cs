using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// Delete action of the Staff Attachment table (spec 7.4 tab): soft delete (CodingRules 7.1) -
// the document bytes stay with the row, exactly like the R-05 icon, so a soft-deleted row
// keeps a consistent File column group. The explicit CompanyId match scopes the row to the
// caller's own company; everything else answers 404.
public record DeleteStaffAttachmentCommand(Guid Id) : IRequest;

public class DeleteStaffAttachmentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteStaffAttachmentCommand>
{
    public async Task Handle(DeleteStaffAttachmentCommand request, CancellationToken ct)
    {
        var entity = await db.StaffAttachments
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff attachment not found.");

        db.StaffAttachments.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
