using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.HalalCertificate;

// "List of Halal Certificate" (spec 12.8 [CONFIRMED] columns): Action is client side, the
// rest is Certificate Number, Scheme and VH SMART Reference No. from the application, CB
// Reference No. from its batch, the item count (Database.md "Certificate item count" -
// one certificate can cover many rows), Expiry Date and the derived Valid / Expired status.
// The status and the display expiry are computed over the page with HalalStatusCalculator
// (D-04) - never stored, never filtered on - the same shape as the Premise Document Status
// in GetAllPremises; "today" is the shared Asia/Kuala_Lumpur date (D-24 /
// PremiseDocumentClock). The spec shows no search box, so only paging and sorting are
// supported; default order is Certificate Number ascending (ours). Scoped to the caller's
// company (PR-01 stance).
public record GetHalalCertificatesQuery
    : IRequest<DataGridResponse<GetHalalCertificatesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetHalalCertificatesResponse(
    Guid Id,
    string CertificateNumber,
    string Scheme,
    string ReferenceNo,
    string? CbReferenceNo,
    int NoCertificateItem,
    DateOnly? ExpiryDate,
    string HalalCertificateStatus);

public class GetHalalCertificatesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetHalalCertificatesQuery, DataGridResponse<GetHalalCertificatesResponse>>
{
    public async Task<DataGridResponse<GetHalalCertificatesResponse>> Handle(
        GetHalalCertificatesQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(GetHalalCertificatesResponse.CertificateNumber)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort ? false : request.Request.SortDescending;

        var grid = await db.HalalCertificates
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GetHalalCertificatesResponse(
                row.Id,
                row.CertificateNo,
                db.Schemes
                    .Where(scheme => scheme.Id == db.Applications
                        .Where(application => application.Id == row.ApplicationId)
                        .Select(application => application.SchemeId)
                        .FirstOrDefault())
                    .Select(scheme => scheme.Name)
                    .FirstOrDefault() ?? string.Empty,
                db.Applications
                    .Where(application => application.Id == row.ApplicationId)
                    .Select(application => application.ReferenceNo)
                    .FirstOrDefault() ?? string.Empty,
                // The batch's CB reference ("CB Reference No." column, Database.md 10
                // AppBatches); null when the application has no batch or the batch has no
                // reference yet. An empty Guid subquery matches no batch row.
                db.Batches
                    .Where(batch => batch.Id == db.Applications
                        .Where(application => application.Id == row.ApplicationId)
                        .Select(application => application.BatchId)
                        .FirstOrDefault())
                    .Select(batch => batch.CbReferenceNo)
                    .FirstOrDefault(),
                db.CertificateItems
                    .Count(item => item.HalalCertificateId == row.Id),
                row.ExpiryDate,
                string.Empty))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        if (!grid.Data.Any())
            return grid;

        var today = PremiseDocumentClock.Today();
        grid.Data = [.. grid.Data.Select(row => row with
        {
            ExpiryDate = HalalStatusCalculator.ExpiryForDisplay(row.ExpiryDate),
            HalalCertificateStatus =
                HalalStatusCalculator.Status(row.ExpiryDate, today).ToString()
        })];

        return grid;
    }
}
