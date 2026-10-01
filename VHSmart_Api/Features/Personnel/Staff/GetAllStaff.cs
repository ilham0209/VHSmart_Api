using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Personnel.Staff;

// All Staff list (spec 7.4, CONFIRMED columns): Action (view/edit/delete - client side, only
// the Id is needed), Name, Designation, Contact Info (Email, Office No., Mobile No.), Company.
// The explicit CompanyId match keeps a ViewAllCompanies caller on their own rows - the tenant
// filter alone is wide open for them (same guard as CI-01/CI-02). The "Switch Company" control
// of the legacy screen is not replicated (see the task report); the Company cell always shows
// the caller's own company. Search covers Name and Email like the Manage Users list. Default
// order is Name ascending, the "#" column renumbers over the whole list.
public record GetAllStaffQuery : IRequest<DataGridResponse<GetAllStaffResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllStaffResponse(
    Guid Id,
    int No,
    string Name,
    string Designation,
    string Email,
    string? OfficeNumber,
    string? MobileNumber,
    string Company);

public class GetAllStaffHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllStaffQuery, DataGridResponse<GetAllStaffResponse>>
{
    public async Task<DataGridResponse<GetAllStaffResponse>> Handle(
        GetAllStaffQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort ? nameof(GetAllStaffResponse.Name) : request.Request.SortBy;

        var response = await (
                from staff in db.Staffs.AsNoTracking()
                    .Where(x => x.CompanyId == user.CompanyId)
                select new GetAllStaffResponse(
                    staff.Id,
                    0,
                    staff.Name,
                    staff.Designation == null ? string.Empty : staff.Designation.Name,
                    staff.Email,
                    staff.OfficeNumber,
                    staff.MobileNumber,
                    (from company in db.Companies
                     where company.Id == staff.CompanyId
                     select company.Name).FirstOrDefault() ?? string.Empty))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllStaffResponse.Name),
                nameof(GetAllStaffResponse.Email))
            .ApplySort(sortBy, request.Request.SortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // "No." is position in the whole list, not on the page (spec 7.4 first column).
        var offset = (response.CurrentPage - 1) * response.PageSize;
        response.Data = response.Data
            .Select((row, index) => row with { No = offset + index + 1 })
            .ToList();

        return response;
    }
}
