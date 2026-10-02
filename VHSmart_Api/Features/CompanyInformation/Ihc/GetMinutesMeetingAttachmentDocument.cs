using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// Download of one meeting attachment (the Action cell of the spec 7.6 attachment list):
// streams the stored document inline. Gated by the same Company.InternalHalalCommittee View
// permission as the list (CodingRules 10); the row must belong to the meeting in the route
// and to the caller's company. An attachment stored without bytes (Document is optional,
// Database.md 4) answers the same 404 as missing storage, exactly like the staff attachment
// download.
public record GetMinutesMeetingAttachmentDocumentQuery(Guid MinutesMeetingId, Guid AttachmentId)
    : IRequest<GetMinutesMeetingAttachmentDocumentResponse>;

public record GetMinutesMeetingAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetMinutesMeetingAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<
        GetMinutesMeetingAttachmentDocumentQuery,
        GetMinutesMeetingAttachmentDocumentResponse>
{
    public async Task<GetMinutesMeetingAttachmentDocumentResponse> Handle(
        GetMinutesMeetingAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.MinutesMeetingAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.MinutesMeetingId == request.MinutesMeetingId
                    && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Minutes meeting attachment not found.");

        var document = entity.Document;
        if (document is null || string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return new GetMinutesMeetingAttachmentDocumentResponse(content, contentType);
    }
}
