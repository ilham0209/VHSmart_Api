using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "List of Application" (spec 12.3, [CONFIRMED] columns): Action is client side (only the
// Id is needed), Type is ApplicationType, Company Name and Scheme are joined display names,
// CB Application No. is the row's own column (null until submit fills it), Halal Expiry
// Date belongs to the certificate (AppHalalCertificates, HA-06) so it is always null for
// now - the column ships because the spec screen shows it. Status / Status Date are the
// D-26 pair. Scheme and BatchName are two fields so the column stays sortable; the screen
// renders "Scheme (with batch name)" from both. The spec shows no search box, so only
// paging and sorting are supported; default order is Status Date descending (the spec
// states no order - ours). The explicit CompanyId match keeps even a Switch Company = ALL
// caller on their own rows (PR-01 stance).
public record GetAllApplicationsQuery : IRequest<DataGridResponse<GetAllApplicationsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllApplicationsResponse(
    Guid Id,
    string Type,
    string CompanyName,
    string ReferenceNo,
    string Scheme,
    string? BatchName,
    string? CbApplicationNo,
    DateTime? HalalExpiryDate,
    string Status,
    DateTime StatusDate);

public class GetAllApplicationsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllApplicationsQuery, DataGridResponse<GetAllApplicationsResponse>>
{
    public async Task<DataGridResponse<GetAllApplicationsResponse>> Handle(
        GetAllApplicationsQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(GetAllApplicationsResponse.StatusDate)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort || request.Request.SortDescending;

        return await db.Applications
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GetAllApplicationsResponse(
                row.Id,
                row.ApplicationType,
                db.Companies
                    .Where(company => company.Id == row.CompanyId)
                    .Select(company => company.Name)
                    .FirstOrDefault() ?? string.Empty,
                row.ReferenceNo,
                db.Schemes
                    .Where(scheme => scheme.Id == row.SchemeId)
                    .Select(scheme => scheme.Name)
                    .FirstOrDefault() ?? string.Empty,
                row.BatchId == null
                    ? null
                    : db.Batches
                        .Where(batch => batch.Id == row.BatchId)
                        .Select(batch => batch.Name)
                        .FirstOrDefault(),
                row.CbApplicationNo,
                // AppHalalCertificates does not exist until HA-06 - the spec column is
                // always null for now (same stance as CB Application No. in HA-01's list).
                null,
                row.Status,
                row.StatusDate))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
