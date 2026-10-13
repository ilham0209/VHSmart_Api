using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// Form save of a new Other payment (spec 15.2): Payment Category* is a General Data row of
// the caller's company (Group PAYMENT, the "Payment Category" category of spec 5.1) and
// Payee* is a Service Provider of the same company (Admin > Reference Data > Service
// Provider); both are prerequisites the form cannot fill on its own [MANUAL]. Amount*, the
// typed Reference No. / Bank Name / Bank Account No., Submitted By* + Submitted Date* and
// Received By* + Received Date* follow the Certificate form (Database.md 12: all four staff /
// date values required from creation). PaymentVoucherNo is typed and optional (D-23).
// CreatedDate is SysDateCreated, stamped by the interceptor - the spec's "today, not
// editable". Status starts at the first step of the D-23 Other chain ("REQUEST BANK DRAFT TO
// FINANCE DEPT"); step 3 is PAYMENT TO SERVICE PROVIDER, never PAYMENT TO CB (spec 15.2).
// Payment does NOT change any other module's status (spec 15.3 - effects none). A foreign
// category, payee or staff member is a 400 from the validator. Behind Payment.Other Create.
public record CreateOtherPaymentCommand(
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
    string? PaymentVoucherNo) : IRequest<OtherPaymentDetailResponse>;

public class CreateOtherPaymentValidator
    : AbstractValidator<CreateOtherPaymentCommand>
{
    // The Category literal of spec 5.1 / GeneralDataCatalog (PAYMENT group).
    private const string PaymentCategoryName = "Payment Category";

    public CreateOtherPaymentValidator(VHSmartDbContext db, ICurrentUser user)
    {
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
    }
}

public class CreateOtherPaymentHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<CreateOtherPaymentCommand, OtherPaymentDetailResponse>
{
    public async Task<OtherPaymentDetailResponse> Handle(
        CreateOtherPaymentCommand request,
        CancellationToken ct)
    {
        var payment = new OtherPaymentEntity
        {
            CompanyId = user.CompanyId,
            PaymentCategoryId = request.PaymentCategoryId,
            ServiceProviderId = request.ServiceProviderId,
            Amount = request.Amount,
            ReferenceNo = request.ReferenceNo,
            BankName = request.BankName,
            BankAccountNo = request.BankAccountNo,
            SubmittedByStaffId = request.SubmittedByStaffId,
            SubmittedDate = request.SubmittedDate,
            ReceivedByStaffId = request.ReceivedByStaffId,
            ReceivedDate = request.ReceivedDate,
            PaymentVoucherNo = request.PaymentVoucherNo,
            Status = PaymentStatusPolicy.InitialStatus(PaymentType.Other)
        };
        db.Payments.Add(payment);

        await db.SaveChangesAsync(ct);

        return await OtherPaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}
