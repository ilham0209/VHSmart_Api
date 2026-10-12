using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// "VH SMART™ Reference No." link-lookup of the payment form (spec 15.1): every live
// application of the caller's company with the fields the form auto-fills from it - CB
// (the company's Certification Body, one value per tenant - read live, Database.md note),
// CB Reference No. (the application's CbApplicationNo - naming flag), Scheme, Application
// Type, Brand and Application Status. The spec states no status filter, so a draft is as
// selectable as an approved one (flagged). Brand uses the PaymentQueries.BrandOfBatchAsync
// derivation (first ACTIVE batch product; batch-less applications answer null - flagged).
// Default order: Reference No. descending (newest first - the D-09 generator hands out one
// number per scheme + date). Behind Payment.Certificate View.
public record GetCertificatePaymentApplicationLookupQuery
    : IRequest<DataGridResponse<CertificatePaymentApplicationResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record CertificatePaymentApplicationResponse(
    Guid Id,
    string ReferenceNo,
    string? CbApplicationNo,
    string Scheme,
    string ApplicationType,
    string? Brand,
    string? CertificationBody,
    string Status);

public class GetCertificatePaymentApplicationLookupHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<
        GetCertificatePaymentApplicationLookupQuery,
        DataGridResponse<CertificatePaymentApplicationResponse>>
{
    public async Task<DataGridResponse<CertificatePaymentApplicationResponse>> Handle(
        GetCertificatePaymentApplicationLookupQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(CertificatePaymentApplicationResponse.ReferenceNo)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort || request.Request.SortDescending;

        return await db.Applications
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new CertificatePaymentApplicationResponse(
                row.Id,
                row.ReferenceNo,
                row.CbApplicationNo,
                db.Schemes
                    .Where(scheme => scheme.Id == row.SchemeId)
                    .Select(scheme => scheme.Name)
                    .FirstOrDefault() ?? string.Empty,
                row.ApplicationType,
                db.BatchProducts
                    .Where(link => link.BatchId == row.BatchId
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
                db.Companies
                    .Where(company => company.Id == row.CompanyId)
                    .Select(company => db.CertificationBodies
                        .Where(cb => cb.Id == company.CertificationBodyId)
                        .Select(cb => cb.Name)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                row.Status))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(CertificatePaymentApplicationResponse.ReferenceNo),
                nameof(CertificatePaymentApplicationResponse.CbApplicationNo),
                nameof(CertificatePaymentApplicationResponse.Scheme),
                nameof(CertificatePaymentApplicationResponse.ApplicationType),
                nameof(CertificatePaymentApplicationResponse.Brand),
                nameof(CertificatePaymentApplicationResponse.CertificationBody),
                nameof(CertificatePaymentApplicationResponse.Status))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
