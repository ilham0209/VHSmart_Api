using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Personnel.Staff;

// Delete a staff row (spec 7.4 list carries a delete action): soft delete only (CodingRules
// 7.1); the attachments stay with the row and the list stops showing it, exactly like every
// other delete in this rebuild. The explicit CompanyId match keeps a ViewAll caller on its own
// rows; everything else answers 404. The legacy "All Staff deleted" in-app notification (21.8)
// is not sent - no recipient rule exists (see the task report).
public record DeleteStaffCommand(Guid Id) : IRequest;

public class DeleteStaffHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteStaffCommand>
{
    public async Task Handle(DeleteStaffCommand request, CancellationToken ct)
    {
        var entity = await db.Staffs.FirstOrDefaultAsync(
            row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff not found.");

        db.Staffs.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
