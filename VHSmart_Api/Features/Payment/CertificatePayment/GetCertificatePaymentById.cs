using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Detail of the certificate payment edit page (spec 15.1 form): the row's own values plus
// the read-throughs Database.md 12 names - CB, CB Reference No. (= the application's
// CbApplicationNo - naming flag), Scheme, Application Type, Brand and Application Status all
// come from the linked application, never copied onto the payment. Submitted By / Received
// By render their staff names. A missing (soft-deleted) application renders empty strings
// rather than 404: the payment row is the 404 key. Behind Payment.Certificate View.
public record GetCertificatePaymentByIdQuery(Guid Id)
    : IRequest<CertificatePaymentDetailResponse>;

public record CertificatePaymentDetailResponse(
    Guid Id,
    Guid HalalApplicationId,
    string ApplicationReferenceNo,
    string? CbApplicationNo,
    string ApplicationType,
    string Scheme,
    string? Batch,
    string? Brand,
    string? CertificationBody,
    string ApplicationStatus,
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
    DateOnly? DateHalalApplication,
    DateOnly? ChargeLetterDate,
    DateTime CreatedDate);

public class GetCertificatePaymentByIdValidator
    : AbstractValidator<GetCertificatePaymentByIdQuery>
{
    public GetCertificatePaymentByIdValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetCertificatePaymentByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCertificatePaymentByIdQuery, CertificatePaymentDetailResponse>
{
    public async Task<CertificatePaymentDetailResponse> Handle(
        GetCertificatePaymentByIdQuery request,
        CancellationToken ct)
    {
        var payment = await PaymentQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Certificate payment not found.");

        return await PaymentQueries.ToDetailAsync(db, user, payment, ct);
    }
}

// Shared helpers of the Payment > Certificate features (and the shape PY-02's Other screen
// can reuse). Tenant scope is the explicit CompanyId match house style requires even for a
// ViewAll caller (PR-01 stance).
internal static class PaymentQueries
{
    public static IQueryable<CertificatePaymentEntity> Own(
        VHSmartDbContext db,
        ICurrentUser user) =>
        db.Payments.OfType<CertificatePaymentEntity>()
            .Where(row => row.CompanyId == user.CompanyId);

    // Brand of the linked application = the first ACTIVE batch product's brand GeneralData
    // name (Database.md 12 "brand is read from the linked application"; the product tab of
    // spec 12.5 defines the same derivation). A batch-less application (food premises) has
    // no products and answers null (flagged); multiple brands in one batch answer the first
    // by link id (spec silent, flagged).
    public static async Task<string?> BrandOfBatchAsync(
        VHSmartDbContext db,
        Guid? batchId,
        CancellationToken ct)
    {
        if (batchId is null)
            return null;

        return await db.BatchProducts.AsNoTracking()
            .Where(link => link.BatchId == batchId
                && link.MappingStatus == BatchProductMappingStatus.Active)
            .OrderBy(link => link.Id)
            .Select(link => db.Products
                .Where(product => product.Id == link.ProductId)
                .Select(product => db.GeneralData
                    .Where(data => data.Id == product.BrandId)
                    .Select(data => data.Name)
                    .FirstOrDefault())
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);
    }

    public static async Task<CertificatePaymentDetailResponse> ToDetailAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        CertificatePaymentEntity payment,
        CancellationToken ct)
    {
        var application = await db.Applications.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == payment.HalalApplicationId, ct);

        var applicationReferenceNo = string.Empty;
        string? cbApplicationNo = null;
        var applicationType = string.Empty;
        var applicationStatus = string.Empty;
        var scheme = string.Empty;
        string? batch = null;
        string? brand = null;

        if (application is not null)
        {
            applicationReferenceNo = application.ReferenceNo;
            cbApplicationNo = application.CbApplicationNo;
            applicationType = application.ApplicationType;
            applicationStatus = application.Status;
            scheme = await db.Schemes.AsNoTracking()
                    .Where(row => row.Id == application.SchemeId)
                    .Select(row => row.Name)
                    .FirstOrDefaultAsync(ct) ?? string.Empty;
            batch = application.BatchId is null
                ? null
                : await db.Batches.AsNoTracking()
                    .Where(row => row.Id == application.BatchId)
                    .Select(row => row.Name)
                    .FirstOrDefaultAsync(ct);
            brand = await BrandOfBatchAsync(db, application.BatchId, ct);
        }

        var certificationBody = await db.Companies.AsNoTracking()
            .Where(row => row.Id == payment.CompanyId)
            .Select(row => db.CertificationBodies
                .Where(cb => cb.Id == row.CertificationBodyId)
                .Select(cb => cb.Name)
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);

        var submittedBy = await db.Staffs.AsNoTracking()
            .Where(row => row.Id == payment.SubmittedByStaffId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var receivedBy = await db.Staffs.AsNoTracking()
            .Where(row => row.Id == payment.ReceivedByStaffId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new CertificatePaymentDetailResponse(
            payment.Id,
            payment.HalalApplicationId,
            applicationReferenceNo,
            cbApplicationNo,
            applicationType,
            scheme,
            batch,
            brand,
            certificationBody,
            applicationStatus,
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
            payment.DateHalalApplication,
            payment.ChargeLetterDate,
            payment.SysDateCreated);
    }
}
