using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Delete action of the Minutes Meeting list (spec 7.6, Action column: view, edit, delete):
// soft delete (CodingRules 7.1). The attachment rows stay with the meeting, exactly like the
// staff attachments P-01 left behind - the meeting detail (their only list) disappears with
// the parent. The row must belong to the caller's own company; everything else answers 404.
public record DeleteMinutesMeetingCommand(Guid Id) : IRequest;

public class DeleteMinutesMeetingHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteMinutesMeetingCommand>
{
    public async Task Handle(DeleteMinutesMeetingCommand request, CancellationToken ct)
    {
        var entity = await db.MinutesMeetings.FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Minutes meeting not found.");

        db.MinutesMeetings.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
