using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Form save of a new certificate payment (spec 15.1): the VH SMART Reference No. link
// (HalalApplicationId - the read-throughs are computed from it, Database.md 12), the two
// optional letter dates, Amount*, the typed Reference No. / Bank Name / Bank Account No.,
// Submitted By* + Submitted Date* and Received By* + Received Date* (all four required from
// creation per Database.md 12). PaymentVoucherNo is typed and optional (D-23). CreatedDate
// is SysDateCreated, stamped by the interceptor - the spec's "today, not editable". Status
// starts at the first step of the D-23 chain ("REQUEST BANK DRAFT TO FINANCE DEPT").
// Payment does NOT change the linked application's status (spec 15.3 - effects none). A
// foreign application or staff member is a 400 from the validator; the payment is saved and
// the detail read-throughs are returned. Behind Payment.Certificate Create.
public record CreateCertificatePaymentCommand(
    Guid HalalApplicationId,
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
    string? PaymentVoucherNo) : IRequest<CertificatePaymentDetailResponse>;

public class CreateCertificatePaymentValidator
    : AbstractValidator<CreateCertificatePaymentCommand>
{
    public CreateCertificatePaymentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.HalalApplicationId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Halal Application is required.")
            .MustAsync((id, ct) => db.Applications.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Halal Application not found.");

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
    }
}

public class CreateCertificatePaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateCertificatePaymentCommand, CertificatePaymentDetailResponse>
{
    public async Task<CertificatePaymentDetailResponse> Handle(
        CreateCertificatePaymentCommand request,
        CancellationToken ct)
    {
        var payment = new CertificatePaymentEntity
        {
            CompanyId = user.CompanyId,
            HalalApplicationId = request.HalalApplicationId,
            DateHalalApplication = request.DateHalalApplication,
            ChargeLetterDate = request.ChargeLetterDate,
            Amount = request.Amount,
            ReferenceNo = request.ReferenceNo,
            BankName = request.BankName,
            BankAccountNo = request.BankAccountNo,
            SubmittedByStaffId = request.SubmittedByStaffId,
            SubmittedDate = request.SubmittedDate,
            ReceivedByStaffId = request.ReceivedByStaffId,
            ReceivedDate = request.ReceivedDate,
            PaymentVoucherNo = request.PaymentVoucherNo,
            Status = PaymentStatusPolicy.InitialStatus(PaymentType.Certificate)
        };
        db.Payments.Add(payment);

        await db.SaveChangesAsync(ct);

        return await PaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}
