using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.HalalApplication.HalalCertificate;

// Download of the uploaded certificate file (the view action of the spec 12.8 list): the
// stored file streamed inline, scoped to the certificate and the tenant filter (404 for an
// unknown or foreign id). A certificate whose step-5 upload has not happened yet answers
// 404 "No document." - same stance as the other downloads in this codebase.
public record GetHalalCertificateDocumentQuery(Guid Id)
    : IRequest<GetHalalCertificateDocumentResponse>;

public record GetHalalCertificateDocumentResponse(Stream Content, string ContentType);

public class GetHalalCertificateDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetHalalCertificateDocumentQuery, GetHalalCertificateDocumentResponse>
{
    public async Task<GetHalalCertificateDocumentResponse> Handle(
        GetHalalCertificateDocumentQuery request,
        CancellationToken ct)
    {
        var certificate = await db.HalalCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Halal certificate not found.");

        var document = certificate.Document;
        if (document is null || string.IsNullOrWhiteSpace(document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return new GetHalalCertificateDocumentResponse(content, contentType);
    }
}
