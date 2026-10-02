using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Download of one Premise Attachment row (the Action cell of the spec 7.7 attachment table):
// streams the stored PDF inline, scoped to the premise in the route and to the caller's
// company (404 otherwise - out-of-scope ids are never 403). The row's Document is required
// (Database.md 7), but a missing storage file still answers 404 like every other download.
public record GetPremiseAttachmentDocumentQuery(Guid PremiseId, Guid AttachmentId)
    : IRequest<GetPremiseAttachmentDocumentResponse>;

public record GetPremiseAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetPremiseAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetPremiseAttachmentDocumentQuery, GetPremiseAttachmentDocumentResponse>
{
    public async Task<GetPremiseAttachmentDocumentResponse> Handle(
        GetPremiseAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.PremiseAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.PremiseId == request.PremiseId
                    && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Premise attachment not found.");

        if (string.IsNullOrWhiteSpace(entity.Document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(entity.Document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(entity.Document.ContentType)
            ? "application/octet-stream"
            : entity.Document.ContentType;

        return new GetPremiseAttachmentDocumentResponse(content, contentType);
    }
}
