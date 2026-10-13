using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Delete of a certificate payment (spec 15.1 list Action). Soft delete all round
// (CodingRules 7.1), children first the AU-06 way: the attachment rows (bytes kept - the
// house stance of every attachment delete), then the header. COMPLETED rows are read-only
// (D-23) - 409. Unknown/foreign -> 404. Behind Payment.Certificate Delete.
public record DeleteCertificatePaymentCommand(Guid Id) : IRequest;

public class DeleteCertificatePaymentValidator
    : AbstractValidator<DeleteCertificatePaymentCommand>
{
    public DeleteCertificatePaymentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class DeleteCertificatePaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteCertificatePaymentCommand>
{
    public async Task Handle(DeleteCertificatePaymentCommand request, CancellationToken ct)
    {
        var payment = await PaymentQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Certificate payment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be deleted.");
        }

        var attachments = await db.PaymentAttachments
            .Where(row => row.PaymentId == payment.Id)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            db.PaymentAttachments.Remove(attachment);
        }

        db.Payments.Remove(payment);

        await db.SaveChangesAsync(ct);
    }
}
