using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// Form save of an existing Other payment (spec 15.2 + D-23): every editable form field is
// writable while the row is not COMPLETED - Payment Category, Payee, Amount, the typed bank
// fields, Submitted / Received pairs and PaymentVoucherNo. Unlike the Certificate screen the
// two links are NOT locked after create (flagged: spec silent; they are ordinary dropdown
// fields whose read-throughs are just re-read after a change, with no reference number to
// orphan). Status may move forward exactly ONE step of the Other chain per save (matched
// case-insensitively against the single next step; any other value - a skip, a revert, PAYMENT
// TO CB on an Other row, an unknown text - is a 409). Posting the CURRENT status is not a
// transition: the other fields just save. COMPLETED rows are read-only (409). Unknown/foreign
// -> 404. Behind Payment.Other Edit.
public record UpdateOtherPaymentCommand(
    Guid Id,
    Guid PaymentCategoryId,
    Guid ServiceProviderId,
    decimal Amount,
    string? ReferenceNo,
    string? BankName,
    string? BankAccountNo,
    Guid SubmittedByStaffId,
    DateOnly SubmittedDate,
    Guid ReceivedByStaffId,
    DateOnly ReceivedDate,
    string? PaymentVoucherNo,
    string Status) : IRequest<OtherPaymentDetailResponse>;

public class UpdateOtherPaymentValidator
    : AbstractValidator<UpdateOtherPaymentCommand>
{
    // The Category literal of spec 5.1 / GeneralDataCatalog (PAYMENT group).
    private const string PaymentCategoryName = "Payment Category";

    public UpdateOtherPaymentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.PaymentCategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Payment Category is required.")
            .MustAsync((id, ct) => db.GeneralData.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Group == GeneralDataGroup.PAYMENT
                    && row.Category == PaymentCategoryName,
                ct))
            .WithMessage("Payment Category not found.");

        RuleFor(x => x.ServiceProviderId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Payee is required.")
            .MustAsync((id, ct) => db.ServiceProviders.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Payee not found.");

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

public class UpdateOtherPaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateOtherPaymentCommand, OtherPaymentDetailResponse>
{
    public async Task<OtherPaymentDetailResponse> Handle(
        UpdateOtherPaymentCommand request,
        CancellationToken ct)
    {
        var payment = await OtherPaymentQueries.Own(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Other payment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be edited.");
        }

        if (!string.Equals(payment.Status, request.Status, StringComparison.Ordinal))
        {
            var next = PaymentStatusPolicy.Next(PaymentType.Other, payment.Status);
            if (next is null
                || !string.Equals(next, request.Status, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(
                    "Payment status can only advance one step at a time.");
            }

            payment.Status = next;
        }

        payment.PaymentCategoryId = request.PaymentCategoryId;
        payment.ServiceProviderId = request.ServiceProviderId;
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

        return await OtherPaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}
