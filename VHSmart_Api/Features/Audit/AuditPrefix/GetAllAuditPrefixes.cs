using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// Audit Prefix list (spec 14.2 "Audit Prefix" table, [CONFIRMED]): Action is client side
// (only the Id is needed), Brand is the joined AdmGeneralData name of the row's BrandId, and
// "Modified Date" is SysDateModified (null until first edit). Default order is Modified Date
// descending - the spec's "default sort by Modified Date, newest first" - until the client
// sorts another column. Search covers the entity's own text columns (the same stance as the
// other lists). The explicit CompanyId match keeps a Switch Company = ALL caller on their own
// audit setup rows (spec 14.0: each company's super admin maintains their own).
public record GetAllAuditPrefixesQuery : IRequest<DataGridResponse<GetAllAuditPrefixesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllAuditPrefixesResponse(
    Guid Id,
    string AuditPrefix,
    string Brand,
    string? Description,
    DateTime? ModifiedDate);

public class GetAllAuditPrefixesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllAuditPrefixesQuery, DataGridResponse<GetAllAuditPrefixesResponse>>
{
    public async Task<DataGridResponse<GetAllAuditPrefixesResponse>> Handle(
        GetAllAuditPrefixesQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllAuditPrefixesResponse.ModifiedDate)
            : request.Request.SortBy;
        var descending = defaultSort || request.Request.SortDescending;

        return await (
                from prefix in db.AuditPrefixes.AsNoTracking()
                    .Where(row => row.CompanyId == user.CompanyId)
                select new GetAllAuditPrefixesResponse(
                    prefix.Id,
                    prefix.Prefix,
                    (from brand in db.GeneralData
                     where brand.Id == prefix.BrandId
                     select brand.Name).FirstOrDefault() ?? string.Empty,
                    prefix.Description,
                    prefix.SysDateModified))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllAuditPrefixesResponse.AuditPrefix),
                nameof(GetAllAuditPrefixesResponse.Description))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
