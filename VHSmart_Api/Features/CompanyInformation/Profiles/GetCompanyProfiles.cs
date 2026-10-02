using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.CompanyInformation.Profiles;

// Company > Profiles list (spec 7.3 "Super User view"): Company Id, Company Name,
// Certification Body, Business Registration No., Modified Date, with view/edit per row. The
// spec shows that list to the Serunai Super User, so a platform admin sees every company;
// anyone else sees only the company their token carries, which keeps one endpoint working for
// both the super-user screen and the company user's own Profiles screen without ever leaking
// another tenant (a non-admin can never widen the query - the id match is in the WHERE).
public record GetCompanyProfilesQuery : IRequest<DataGridResponse<GetCompanyProfilesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetCompanyProfilesResponse(
    Guid Id,
    string CompanyName,
    string? CertificationBodyName,
    string BusinessRegistrationNo,
    DateTime? ModifiedDate);

public class GetCompanyProfilesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCompanyProfilesQuery, DataGridResponse<GetCompanyProfilesResponse>>
{
    public async Task<DataGridResponse<GetCompanyProfilesResponse>> Handle(
        GetCompanyProfilesQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetCompanyProfilesResponse.CompanyName)
            : request.Request.SortBy;

        var companies = db.Companies.AsNoTracking();
        if (!user.IsPlatformAdmin)
            companies = companies.Where(company => company.Id == user.CompanyId);

        return await (
                from company in companies
                select new GetCompanyProfilesResponse(
                    company.Id,
                    company.Name,
                    company.CertificationBody == null ? null : company.CertificationBody.Name,
                    company.BusinessRegistrationNo,
                    company.SysDateModified))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetCompanyProfilesResponse.CompanyName),
                nameof(GetCompanyProfilesResponse.BusinessRegistrationNo))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
