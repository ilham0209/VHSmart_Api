using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.Companies;

// Manage Companies list (spec 6.3): one row per linked brand, and a company without a brand
// keeps its row with an empty Brand cell. The Action column (view, edit) is client-side - it
// only needs the company Id. Search and sort run on company columns in SQL; the brand
// expansion and the paging happen afterwards on the loaded rows (the table is company-sized),
// so the grid totals count the expanded rows.
public record GetAllCompaniesQuery : IRequest<DataGridResponse<GetAllCompaniesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllCompaniesResponse(
    Guid Id,
    string Name,
    string Email,
    string? CertificationBody,
    string BusinessRegistrationNo,
    string? Brand,
    DateTime? ModifiedDate);

public class GetAllCompaniesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllCompaniesQuery, DataGridResponse<GetAllCompaniesResponse>>
{
    public async Task<DataGridResponse<GetAllCompaniesResponse>> Handle(
        GetAllCompaniesQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage companies.");

        var companies = await db.Companies
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(CompanyEntity.Name),
                nameof(CompanyEntity.Email),
                nameof(CompanyEntity.BusinessRegistrationNo))
            .Include(company => company.CertificationBody)
            .Include(company => company.Brands)
                .ThenInclude(link => link.Brand)
            .ToListAsync(ct);

        var expanded = companies
            .AsQueryable()
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .SelectMany(company => Expand(company))
            .ToList();

        // The page is sliced here rather than through ToDataGridResponseAsync: that helper
        // counts through EF, while the brand expansion above is a plain in-memory sequence.
        // The company table is small, and the clamping mirrors the shared helper exactly.
        var pageSize = request.Request.PageSize <= 0
            ? DataGridRequest.DefaultPageSize
            : Math.Min(request.Request.PageSize, DataGridRequest.MaxPageSize);
        var page = request.Request.Page <= 0 ? 1 : request.Request.Page;
        var totalRecords = expanded.Count;
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        var offset = (int)Math.Min((page - 1) * (long)pageSize, int.MaxValue);

        return new DataGridResponse<GetAllCompaniesResponse>
        {
            Data = expanded.Skip(offset).Take(pageSize),
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            CurrentPage = page,
            PageSize = pageSize,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    // One row per linked brand; a company whose links are all soft-deleted still yields a
    // single row with a null Brand, like a company that never had one.
    private static IEnumerable<GetAllCompaniesResponse> Expand(CompanyEntity company)
    {
        var any = false;
        foreach (var link in company.Brands
                     .OrderBy(link => link.Brand?.Name, StringComparer.Ordinal))
        {
            if (link.Brand is null)
                continue;

            any = true;
            yield return Row(company, link.Brand.Name);
        }

        if (!any)
            yield return Row(company, null);
    }

    private static GetAllCompaniesResponse Row(CompanyEntity company, string? brand) =>
        new(
            company.Id,
            company.Name,
            company.Email,
            company.CertificationBody?.Name,
            company.BusinessRegistrationNo,
            brand,
            company.SysDateModified);
}
