using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.CompanyInformation.HalalPolicy;

// Halal Policy list (spec 7.2 "#, Scheme, File Name, Policy Date, Action"): the Action column
// (download / delete) is client-side; the bytes stream from GET {id}/document. The explicit
// CompanyId match keeps a ViewAllCompanies caller on their own rows - the tenant filter alone
// is wide open for them (same guard as the subscription history). Search covers the scheme
// name and the file name; the default order is the newest policy date first.
public record GetAllHalalPoliciesQuery : IRequest<DataGridResponse<GetAllHalalPoliciesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllHalalPoliciesResponse(
    Guid Id,
    int No,
    string Scheme,
    string FileName,
    DateTime PolicyDate);

public class GetAllHalalPoliciesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllHalalPoliciesQuery, DataGridResponse<GetAllHalalPoliciesResponse>>
{
    public async Task<DataGridResponse<GetAllHalalPoliciesResponse>> Handle(
        GetAllHalalPoliciesQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllHalalPoliciesResponse.PolicyDate)
            : request.Request.SortBy;
        var descending = defaultSort || request.Request.SortDescending;

        var response = await (
                from policy in db.HalalPolicies.AsNoTracking()
                    .Where(x => x.CompanyId == user.CompanyId)
                select new GetAllHalalPoliciesResponse(
                    policy.Id,
                    0,
                    policy.Scheme == null ? string.Empty : policy.Scheme.Name,
                    policy.Document.FileName,
                    policy.PolicyDate))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllHalalPoliciesResponse.Scheme),
                nameof(GetAllHalalPoliciesResponse.FileName))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);

        // "No." is position in the whole list, not on the page (spec 7.2 first column).
        var offset = (response.CurrentPage - 1) * response.PageSize;
        response.Data = response.Data
            .Select((row, index) => row with { No = offset + index + 1 })
            .ToList();

        return response;
    }
}
