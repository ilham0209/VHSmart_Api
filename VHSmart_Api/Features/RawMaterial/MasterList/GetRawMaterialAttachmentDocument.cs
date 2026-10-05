using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Download of one Attachment Information row (the Action cell of the spec 10.2 section): the
// stored file streamed inline, scoped to the raw material in the route and to the tenant filter
// (404 for an unknown, foreign or mismatched id - out-of-scope ids are never 403). A missing
// storage file answers 404 like every other download in this codebase.
public record GetRawMaterialAttachmentDocumentQuery(Guid RawMaterialId, Guid AttachmentId)
    : IRequest<GetRawMaterialAttachmentDocumentResponse>;

public record GetRawMaterialAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetRawMaterialAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetRawMaterialAttachmentDocumentQuery, GetRawMaterialAttachmentDocumentResponse>
{
    public async Task<GetRawMaterialAttachmentDocumentResponse> Handle(
        GetRawMaterialAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.RawMaterialAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.RawMaterialId == request.RawMaterialId
                    && row.CompanyId == user.CompanyId,
                ct)
            ?? throw new NotFoundException("Raw material attachment not found.");

        if (string.IsNullOrWhiteSpace(entity.Document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(entity.Document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(entity.Document.ContentType)
            ? "application/octet-stream"
            : entity.Document.ContentType;

        return new GetRawMaterialAttachmentDocumentResponse(content, contentType);
    }
}
