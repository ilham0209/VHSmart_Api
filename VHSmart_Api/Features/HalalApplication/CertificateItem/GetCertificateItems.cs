using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.CertificateItem;

// "List of Certificate Item" (spec 12.8 [CONFIRMED] columns): Action is client side (the Id
// is enough for view / edit), Company Name is a join, Scheme and VH SMART Reference No. come
// from the owning application, Certificate Item is the snapshot name, Brand is a joined
// display name and Certificate Number is null while the row still reads "CLICK TO ADD".
// The spec shows no search box, so only paging and sorting are supported; default order is
// Certificate Item ascending (the spec states no order - ours). Rows are scoped to the
// caller's company (the explicit CompanyId match keeps a Switch Company = ALL caller on
// their own rows, PR-01 stance).
public record GetCertificateItemsQuery : IRequest<DataGridResponse<GetCertificateItemsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetCertificateItemsResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string Scheme,
    string ReferenceNo,
    string CertificateItem,
    string? Brand,
    string? CertificateNumber);

public class GetCertificateItemsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCertificateItemsQuery, DataGridResponse<GetCertificateItemsResponse>>
{
    public async Task<DataGridResponse<GetCertificateItemsResponse>> Handle(
        GetCertificateItemsQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(GetCertificateItemsResponse.CertificateItem)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort ? false : request.Request.SortDescending;

        return await db.CertificateItems
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GetCertificateItemsResponse(
                row.Id,
                row.ApplicationId,
                db.Companies
                    .Where(company => company.Id == row.CompanyId)
                    .Select(company => company.Name)
                    .FirstOrDefault() ?? string.Empty,
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
                row.ItemName,
                row.BrandId == null
                    ? null
                    : db.GeneralData
                        .Where(brand => brand.Id == row.BrandId)
                        .Select(brand => brand.Name)
                        .FirstOrDefault(),
                row.HalalCertificateId == null
                    ? null
                    : db.HalalCertificates
                        .Where(certificate => certificate.Id == row.HalalCertificateId)
                        .Select(certificate => certificate.CertificateNo)
                        .FirstOrDefault()))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
