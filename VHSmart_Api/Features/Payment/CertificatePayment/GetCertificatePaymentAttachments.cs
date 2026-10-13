using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// "List of Attachments" dialog of the payment form (spec 15.1): the row's stored files
// with their typed DocumentType label. Upload date is SysDateCreated (Database.md 12), the
// list is ordered by it (creation order). Unknown/foreign payment -> 404. Behind
// Payment.Certificate View.
public record GetCertificatePaymentAttachmentsQuery(Guid Id)
    : IRequest<IReadOnlyList<CertificatePaymentAttachmentResponse>>;

public record CertificatePaymentAttachmentResponse(
    Guid Id,
    string DocumentType,
    string FileName,
    long SizeBytes,
    DateTime UploadedDate);

public class GetCertificatePaymentAttachmentsValidator
    : AbstractValidator<GetCertificatePaymentAttachmentsQuery>
{
    public GetCertificatePaymentAttachmentsValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetCertificatePaymentAttachmentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<
        GetCertificatePaymentAttachmentsQuery,
        IReadOnlyList<CertificatePaymentAttachmentResponse>>
{
    public async Task<IReadOnlyList<CertificatePaymentAttachmentResponse>> Handle(
        GetCertificatePaymentAttachmentsQuery request,
        CancellationToken ct)
    {
        var paymentOwned = await PaymentQueries.Own(db, user)
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.Id, ct);
        if (!paymentOwned)
        {
            throw new NotFoundException("Certificate payment not found.");
        }

        return await db.PaymentAttachments
            .AsNoTracking()
            .Where(row => row.PaymentId == request.Id && row.CompanyId == user.CompanyId)
            .OrderBy(row => row.SysDateCreated)
            .Select(row => new CertificatePaymentAttachmentResponse(
                row.Id,
                row.DocumentType,
                row.Document.FileName,
                row.Document.SizeBytes,
                row.SysDateCreated))
            .ToListAsync(ct);
    }
}
