using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// "List of External Audit Report and Non Conformance" (spec 14.10 [CONFIRMED]): Action is
// client side; the columns are the report's own values plus joined display texts. Company
// Name is the caller's own company (rows are tenant-scoped, so one value per page -
// fetched once). Premise is the comma-joined names of the report's premise links, filled
// for THIS page after paging (AU-06 Auditor Name stance; not searched). Modified By is
// the SysUserModified stamp as stored (the GetApplicationById stance - it carries the
// user id, flagged), Modified Date is SysDateModified. The spec states no default order,
// so Audit Reference No. ascending is used (first visible data column stance, flagged).
public record GetAllExternalAuditReportsQuery
    : IRequest<DataGridResponse<GetAllExternalAuditReportsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllExternalAuditReportsResponse(
    Guid Id,
    string? AuditReferenceNo,
    string CompanyName,
    string? CbApplicationNo,
    string Premise,
    string ReceivedFrom,
    string? Status,
    string? ModifiedBy,
    DateTime? ModifiedDate);

public class GetAllExternalAuditReportsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllExternalAuditReportsQuery, DataGridResponse<GetAllExternalAuditReportsResponse>>
{
    public async Task<DataGridResponse<GetAllExternalAuditReportsResponse>> Handle(
        GetAllExternalAuditReportsQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllExternalAuditReportsResponse.AuditReferenceNo)
            : request.Request.SortBy;

        var companyName = await db.Companies.AsNoTracking()
            .Where(row => row.Id == user.CompanyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var grid = await db.ExternalAuditReports
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => new GetAllExternalAuditReportsResponse(
                row.Id,
                row.AuditReferenceNo,
                companyName,
                row.CbHalalApplicationNo,
                // Placeholder; the real cell is filled for THIS page after paging.
                string.Empty,
                (from certificationBody in db.CertificationBodies
                 where certificationBody.Id == row.ReceivedFromCertificationBodyId
                 select certificationBody.Name).FirstOrDefault() ?? string.Empty,
                row.Status,
                row.SysUserModified,
                row.SysDateModified))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllExternalAuditReportsResponse.AuditReferenceNo),
                nameof(GetAllExternalAuditReportsResponse.CompanyName),
                nameof(GetAllExternalAuditReportsResponse.CbApplicationNo),
                nameof(GetAllExternalAuditReportsResponse.ReceivedFrom))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var premiseNames = await (
            from link in db.ExternalAuditReportPremises.AsNoTracking()
            where pageIds.Contains(link.ExternalAuditReportId)
            select new
            {
                link.ExternalAuditReportId,
                Name = (from premise in db.Premises
                        where premise.Id == link.PremiseId
                        select premise.Name).FirstOrDefault() ?? string.Empty
            }).ToListAsync(ct);

        var namesByReport = premiseNames
            .GroupBy(row => row.ExternalAuditReportId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ",
                    group
                        .Select(row => row.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));

        grid.Data = [.. grid.Data.Select(row => row with
        {
            Premise = namesByReport.GetValueOrDefault(row.Id, string.Empty)
        })];

        return grid;
    }
}
