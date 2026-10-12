using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Download of one attachment row (the dialog's Action cell, spec 15.1): the stored file
// streamed inline, scoped to the payment in the route and the tenant (404 for an unknown,
// foreign or mismatched id - out-of-scope ids are never 403). A missing storage file
// answers 404 like every other download in this codebase. Behind Payment.Certificate View.
public record GetCertificatePaymentAttachmentDocumentQuery(Guid Id, Guid AttachmentId)
    : IRequest<GetCertificatePaymentAttachmentDocumentResponse>;

public record GetCertificatePaymentAttachmentDocumentResponse(Stream Content, string ContentType);

public class GetCertificatePaymentAttachmentDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<
        GetCertificatePaymentAttachmentDocumentQuery,
        GetCertificatePaymentAttachmentDocumentResponse>
{
    public async Task<GetCertificatePaymentAttachmentDocumentResponse> Handle(
        GetCertificatePaymentAttachmentDocumentQuery request,
        CancellationToken ct)
    {
        var entity = await db.PaymentAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.PaymentId == request.Id
                    && row.CompanyId == user.CompanyId,
                ct)
            ?? throw new NotFoundException("Payment attachment not found.");

        if (string.IsNullOrWhiteSpace(entity.Document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(entity.Document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(entity.Document.ContentType)
            ? "application/octet-stream"
            : entity.Document.ContentType;

        return new GetCertificatePaymentAttachmentDocumentResponse(content, contentType);
    }
}
