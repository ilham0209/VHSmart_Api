using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Form save of an existing certificate payment (spec 15.1 + D-23): every editable form
// field is writable while the row is not COMPLETED - Amount, the typed bank fields,
// Submitted / Received pairs, PaymentVoucherNo and the two letter dates. The VH SMART
// Reference No. link is locked after create (flagged: spec silent; moving it would orphan
// the read-throughs the row is rendered from). Status may move forward exactly ONE step of
// the D-23 chain per save (matched case-insensitively against the single next step; any
// other value - a skip, a revert, an unknown text - is a 409). Posting the CURRENT status
// is not a transition: the other fields just save. COMPLETED rows are read-only (409).
// Unknown/foreign -> 404. Behind Payment.Certificate Edit.
public record UpdateCertificatePaymentCommand(
    Guid Id,
    DateOnly? DateHalalApplication,
    DateOnly? ChargeLetterDate,
    decimal Amount,
    string? ReferenceNo,
    string? BankName,
    string? BankAccountNo,
    Guid SubmittedByStaffId,
    DateOnly SubmittedDate,
    Guid ReceivedByStaffId,
    DateOnly ReceivedDate,
    string? PaymentVoucherNo,
    string Status) : IRequest<CertificatePaymentDetailResponse>;

public class UpdateCertificatePaymentValidator
    : AbstractValidator<UpdateCertificatePaymentCommand>
{
    public UpdateCertificatePaymentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.ReferenceNo)
            .MaximumLength(100)
            .WithMessage("Reference No cannot exceed 100 characters.");

        RuleFor(x => x.BankName)
            .MaximumLength(200)
            .WithMessage("Bank Name cannot exceed 200 characters.");

        RuleFor(x => x.BankAccountNo)
            .MaximumLength(50)
            .WithMessage("Bank Account No cannot exceed 50 characters.");

        RuleFor(x => x.SubmittedByStaffId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Submitted By is required.")
            .MustAsync((id, ct) => db.Staffs.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Submitted By not found.");

        RuleFor(x => x.SubmittedDate)
            .Must(value => value != default)
            .WithMessage("Submitted Date is required.");

        RuleFor(x => x.ReceivedByStaffId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Received By is required.")
            .MustAsync((id, ct) => db.Staffs.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Received By not found.");

        RuleFor(x => x.ReceivedDate)
            .Must(value => value != default)
            .WithMessage("Received Date is required.");

        RuleFor(x => x.PaymentVoucherNo)
            .MaximumLength(50)
            .WithMessage("Payment Voucher No cannot exceed 50 characters.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.");
    }
}

public class UpdateCertificatePaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateCertificatePaymentCommand, CertificatePaymentDetailResponse>
{
    public async Task<CertificatePaymentDetailResponse> Handle(
        UpdateCertificatePaymentCommand request,
        CancellationToken ct)
    {
        var payment = await PaymentQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Certificate payment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be edited.");
        }

        if (!string.Equals(payment.Status, request.Status, StringComparison.Ordinal))
        {
            var next = PaymentStatusPolicy.Next(PaymentType.Certificate, payment.Status);
            if (next is null
                || !string.Equals(next, request.Status, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Payment status can only advance one step at a time.");
            }

            payment.Status = next;
        }

        payment.DateHalalApplication = request.DateHalalApplication;
        payment.ChargeLetterDate = request.ChargeLetterDate;
        payment.Amount = request.Amount;
        payment.ReferenceNo = request.ReferenceNo;
        payment.BankName = request.BankName;
        payment.BankAccountNo = request.BankAccountNo;
        payment.SubmittedByStaffId = request.SubmittedByStaffId;
        payment.SubmittedDate = request.SubmittedDate;
        payment.ReceivedByStaffId = request.ReceivedByStaffId;
        payment.ReceivedDate = request.ReceivedDate;
        payment.PaymentVoucherNo = request.PaymentVoucherNo;

        await db.SaveChangesAsync(ct);

        return await PaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}
