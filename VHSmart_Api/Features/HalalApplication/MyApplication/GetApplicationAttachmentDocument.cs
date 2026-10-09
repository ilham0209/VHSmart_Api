using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Download of one Attachment side-tab row (the Action cell of the spec 12.5 attachment
// table): the stored file streamed inline, scoped to the application in the route and to the
// tenant filter (404 for an unknown, foreign or mismatched id - out-of-scope ids are never
// 403). A missing storage file answers 404 like every other download in this codebase.
public record GetApplicationAttachmentDocumentQuery(Guid ApplicationId, Guid AttachmentId)
    : IRequest<GetApplicationAttachmentDocumentResponse>;

public record GetApplicationAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetApplicationAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetApplicationAttachmentDocumentQuery, GetApplicationAttachmentDocumentResponse>
{
    public async Task<GetApplicationAttachmentDocumentResponse> Handle(
        GetApplicationAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.ApplicationAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.ApplicationId == request.ApplicationId
                    && row.CompanyId == user.CompanyId,
                ct)
            ?? throw new NotFoundException("Application attachment not found.");

        if (string.IsNullOrWhiteSpace(entity.Document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(entity.Document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(entity.Document.ContentType)
            ? "application/octet-stream"
            : entity.Document.ContentType;

        return new GetApplicationAttachmentDocumentResponse(content, contentType);
    }
}
