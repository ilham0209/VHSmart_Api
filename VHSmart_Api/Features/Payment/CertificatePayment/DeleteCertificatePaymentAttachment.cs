using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Delete one attachment (soft delete; the bytes stay in storage - the house stance of
// every attachment delete). The row must belong to a live own-company payment; the payment
// must not be COMPLETED (D-23 read-only) - 409. Unknown/foreign/mismatched -> 404. Behind
// Payment.Certificate Edit.
public record DeleteCertificatePaymentAttachmentCommand(Guid Id, Guid AttachmentId)
    : IRequest;

public class DeleteCertificatePaymentAttachmentValidator
    : AbstractValidator<DeleteCertificatePaymentAttachmentCommand>
{
    public DeleteCertificatePaymentAttachmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.AttachmentId)
            .NotEmpty()
            .WithMessage("Attachment Id is required.");
    }
}

public class DeleteCertificatePaymentAttachmentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteCertificatePaymentAttachmentCommand>
{
    public async Task Handle(
        DeleteCertificatePaymentAttachmentCommand request,
        CancellationToken ct)
    {
        var attachment = await db.PaymentAttachments
            .FirstOrDefaultAsync(
                row => row.Id == request.AttachmentId
                    && row.PaymentId == request.Id
                    && row.CompanyId == user.CompanyId,
                ct)
            ?? throw new NotFoundException("Payment attachment not found.");

        var payment = await PaymentQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Payment attachment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be edited.");
        }

        db.PaymentAttachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
    }
}
