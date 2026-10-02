using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Delete action of the attachment list (spec 7.6, Attachment tab Action column): soft delete
// (CodingRules 7.1) - the document bytes stay with the row, exactly like the staff attachment
// and training module deletes. The row must belong to the meeting in the route AND to the
// caller's own company; everything else answers 404.
public record DeleteMinutesMeetingAttachmentCommand(Guid MinutesMeetingId, Guid AttachmentId)
    : IRequest;

public class DeleteMinutesMeetingAttachmentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteMinutesMeetingAttachmentCommand>
{
    public async Task Handle(
        DeleteMinutesMeetingAttachmentCommand request,
        CancellationToken ct)
    {
        var entity = await db.MinutesMeetingAttachments.FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.MinutesMeetingId == request.MinutesMeetingId
                    && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Minutes meeting attachment not found.");

        db.MinutesMeetingAttachments.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
