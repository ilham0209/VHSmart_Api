using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// Detail of the Other payment edit page (spec 15.2 form): the row's own values plus the two
// read-throughs Database.md 12 names - Payment Category (the linked General Data row's name,
// Group PAYMENT / "Payment Category") and Payee (the linked Service Provider's name) with the
// payee's bank details read live from that provider. Submitted By / Received By render their
// staff names. A missing (soft-deleted) category or provider renders empty rather than 404:
// the payment row is the 404 key. Behind Payment.Other View.
public record GetOtherPaymentByIdQuery(Guid Id)
    : IRequest<OtherPaymentDetailResponse>;

public record OtherPaymentDetailResponse(
    Guid Id,
    Guid PaymentCategoryId,
    string PaymentCategory,
    Guid ServiceProviderId,
    string Payee,
    string? PayeeBankName,
    string? PayeeBankAccountNo,
    string Status,
    decimal Amount,
    string? ReferenceNo,
    string? BankName,
    string? BankAccountNo,
    Guid SubmittedByStaffId,
    string SubmittedBy,
    DateOnly SubmittedDate,
    Guid ReceivedByStaffId,
    string ReceivedBy,
    DateOnly ReceivedDate,
    string? PaymentVoucherNo,
    DateTime CreatedDate);

public class GetOtherPaymentByIdValidator
    : AbstractValidator<GetOtherPaymentByIdQuery>
{
    public GetOtherPaymentByIdValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetOtherPaymentByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetOtherPaymentByIdQuery, OtherPaymentDetailResponse>
{
    public async Task<OtherPaymentDetailResponse> Handle(
        GetOtherPaymentByIdQuery request,
        CancellationToken ct)
    {
        var payment = await OtherPaymentQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Other payment not found.");

        return await OtherPaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}

// Shared helpers of the Payment > Other features. Tenant scope is the explicit CompanyId
// match house style requires even for a ViewAll caller (PR-01 stance, the same stance the
// Certificate helpers take).
internal static class OtherPaymentQueries
{
    public static IQueryable<OtherPaymentEntity> Own(
        VHSmartDbContext db,
        ICurrentUser user) =>
        db.Payments.OfType<OtherPaymentEntity>()
            .Where(row => row.CompanyId == user.CompanyId);

    public static async Task<OtherPaymentDetailResponse> ToDetailAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        OtherPaymentEntity payment,
        CancellationToken ct)
    {
        var paymentCategory = await db.GeneralData.AsNoTracking()
                .Where(row => row.Id == payment.PaymentCategoryId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct) ?? string.Empty;

        var payee = await db.ServiceProviders.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == payment.ServiceProviderId, ct);

        var submittedBy = await db.Staffs.AsNoTracking()
                .Where(row => row.Id == payment.SubmittedByStaffId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct) ?? string.Empty;

        var receivedBy = await db.Staffs.AsNoTracking()
                .Where(row => row.Id == payment.ReceivedByStaffId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new OtherPaymentDetailResponse(
            payment.Id,
            payment.PaymentCategoryId,
            paymentCategory,
            payment.ServiceProviderId,
            payee?.Name ?? string.Empty,
            payee?.BankName,
            payee?.BankAccountNo,
            payment.Status,
            payment.Amount,
            payment.ReferenceNo,
            payment.BankName,
            payment.BankAccountNo,
            payment.SubmittedByStaffId,
            submittedBy,
            payment.SubmittedDate,
            payment.ReceivedByStaffId,
            receivedBy,
            payment.ReceivedDate,
            payment.PaymentVoucherNo,
            payment.SysDateCreated);
    }
}
