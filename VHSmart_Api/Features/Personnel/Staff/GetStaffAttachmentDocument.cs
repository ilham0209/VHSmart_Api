using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Staff;

// Download of one Staff Attachment (the Action cell of the spec 7.4 tab): streams the stored
// document inline. Gated by the same Personnel.AllStaff View permission as the list
// (CodingRules 10); the explicit CompanyId match keeps a ViewAll caller on its own rows and
// LocalFileStorage translates missing bytes into the same 404.
public record GetStaffAttachmentDocumentQuery(Guid Id)
    : IRequest<GetStaffAttachmentDocumentResponse>;

public record GetStaffAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetStaffAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetStaffAttachmentDocumentQuery, GetStaffAttachmentDocumentResponse>
{
    public async Task<GetStaffAttachmentDocumentResponse> Handle(
        GetStaffAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.StaffAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff attachment not found.");

        var document = entity.Document;
        if (string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return new GetStaffAttachmentDocumentResponse(content, contentType);
    }
}
