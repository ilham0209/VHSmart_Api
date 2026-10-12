using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// "List of Certificate Payments" (spec 15.1 [CONFIRMED] columns): Action is client side
// (only the Id is needed). Brand / VH SMART™ Reference No. / CB Reference No. / Scheme /
// Batch / Application Status are the read-throughs of Database.md 12 - computed from the
// linked application, never stored on the payment. CB Reference No. is the application's
// CbApplicationNo (naming flag). Brand is the first ACTIVE batch product's brand (the
// BrandOfBatchAsync derivation); a batch-less application answers null (flagged).
// Amount and Payment Voucher No. are the payment's own columns; Created Date is
// SysDateCreated. The View filter (default "Default") carries no documented values - it is
// accepted and "Default"/empty means no filter, any other value filters Payment Status
// verbatim (flagged). Application Type (default "All") filters the linked application's
// type, "All"/empty meaning no filter. The spec states the default order: Created Date
// newest first. Search covers the visible text columns.
public record GetAllCertificatePaymentsQuery
    : IRequest<DataGridResponse<GetAllCertificatePaymentsResponse>>
{
    public DataGridRequest Request { get; set; } = new();

    public string? View { get; set; }

    public string? ApplicationType { get; set; }
}

public record GetAllCertificatePaymentsResponse(
    Guid Id,
    string? Brand,
    string ApplicationReferenceNo,
    string? CbApplicationNo,
    string Scheme,
    string? Batch,
    decimal Amount,
    string? PaymentVoucherNo,
    string ApplicationStatus,
    string PaymentStatus,
    DateTime CreatedDate);

public class GetAllCertificatePaymentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllCertificatePaymentsQuery, DataGridResponse<GetAllCertificatePaymentsResponse>>
{
    public async Task<DataGridResponse<GetAllCertificatePaymentsResponse>> Handle(
        GetAllCertificatePaymentsQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(GetAllCertificatePaymentsResponse.CreatedDate)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort || request.Request.SortDescending;

        var viewFilter = string.IsNullOrWhiteSpace(request.View)
            || string.Equals(request.View, "Default", StringComparison.OrdinalIgnoreCase)
            ? null
            : request.View;

        var applicationTypeFilter = string.IsNullOrWhiteSpace(request.ApplicationType)
            || string.Equals(request.ApplicationType, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : request.ApplicationType;

        return await db.Payments.OfType<CertificatePaymentEntity>()
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (viewFilter == null || row.Status == viewFilter)
                && (applicationTypeFilter == null
                    || db.Applications.Any(application =>
                        application.Id == row.HalalApplicationId
                        && application.ApplicationType == applicationTypeFilter)))
            .Select(row => new GetAllCertificatePaymentsResponse(
                row.Id,
                // Brand = first ACTIVE batch product's brand (PaymentQueries.BrandOfBatchAsync,
                // inlined for the projection; a batch-less application answers null).
                db.BatchProducts
                    .Where(link => link.BatchId == db.Applications
                        .Where(application => application.Id == row.HalalApplicationId)
                        .Select(application => application.BatchId)
                        .FirstOrDefault()
                        && link.MappingStatus == BatchProductMappingStatus.Active)
                    .OrderBy(link => link.Id)
                    .Select(link => db.Products
                        .Where(product => product.Id == link.ProductId)
                        .Select(product => db.GeneralData
                            .Where(data => data.Id == product.BrandId)
                            .Select(data => data.Name)
                            .FirstOrDefault())
                        .FirstOrDefault())
                    .FirstOrDefault(),
                db.Applications
                    .Where(application => application.Id == row.HalalApplicationId)
                    .Select(application => application.ReferenceNo)
                    .FirstOrDefault() ?? string.Empty,
                db.Applications
                    .Where(application => application.Id == row.HalalApplicationId)
                    .Select(application => application.CbApplicationNo)
                    .FirstOrDefault(),
                db.Schemes
                    .Where(scheme => scheme.Id == db.Applications
                        .Where(application => application.Id == row.HalalApplicationId)
                        .Select(application => application.SchemeId)
                        .FirstOrDefault())
                    .Select(scheme => scheme.Name)
                    .FirstOrDefault() ?? string.Empty,
                db.Batches
                    .Where(batch => batch.Id == db.Applications
                        .Where(application => application.Id == row.HalalApplicationId)
                        .Select(application => application.BatchId)
                        .FirstOrDefault())
                    .Select(batch => batch.Name)
                    .FirstOrDefault(),
                row.Amount,
                row.PaymentVoucherNo,
                db.Applications
                    .Where(application => application.Id == row.HalalApplicationId)
                    .Select(application => application.Status)
                    .FirstOrDefault() ?? string.Empty,
                row.Status,
                row.SysDateCreated))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllCertificatePaymentsResponse.Brand),
                nameof(GetAllCertificatePaymentsResponse.ApplicationReferenceNo),
                nameof(GetAllCertificatePaymentsResponse.CbApplicationNo),
                nameof(GetAllCertificatePaymentsResponse.Scheme),
                nameof(GetAllCertificatePaymentsResponse.Batch),
                nameof(GetAllCertificatePaymentsResponse.PaymentVoucherNo),
                nameof(GetAllCertificatePaymentsResponse.ApplicationStatus),
                nameof(GetAllCertificatePaymentsResponse.PaymentStatus))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
