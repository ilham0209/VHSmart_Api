using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Premise List (spec 7.7, CONFIRMED columns): Action is client side (only the Id is needed);
// Premise Information (Store Code, Store Name, Address, City, State, Country, Phone No.,
// Email), Representative (Area Manager, Operation Manager), Brand and Premise Type come from
// the row. Halal Information, Document Status and Tag are NOT selected yet - their tables
// arrive with PR-02 (attachments/Tag) and PR-04/HA (halal joins) per the TaskList row.
// The "Premise Type" toolbar filter (ALL / specific) binds as a nullable enum: null = ALL.
// Search covers the visible Premise Information columns; the spec states no default order,
// so StoreName ascending is used (flagged in the report). The explicit CompanyId match keeps
// a ViewAll caller on their own rows (same guard as every other list).
public record GetAllPremisesQuery : IRequest<DataGridResponse<GetAllPremisesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
    public PremiseType? PremiseType { get; set; }
}

public record GetAllPremisesResponse(
    Guid Id,
    string StoreCode,
    string StoreName,
    string Address,
    string City,
    string State,
    string Country,
    string Telephone,
    string Email,
    string AreaManager,
    string OperationManager,
    string Brand,
    PremiseType PremiseType);

public class GetAllPremisesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllPremisesQuery, DataGridResponse<GetAllPremisesResponse>>
{
    public async Task<DataGridResponse<GetAllPremisesResponse>> Handle(
        GetAllPremisesQuery request,
        CancellationToken ct)
    {
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(GetAllPremisesResponse.StoreName)
            : request.Request.SortBy;
        var sortDescending = request.Request.SortDescending;

        return await (
                from premise in db.Premises.AsNoTracking()
                    .Where(x => x.CompanyId == user.CompanyId
                        && (request.PremiseType == null || x.PremiseType == request.PremiseType))
                select new GetAllPremisesResponse(
                    premise.Id,
                    premise.StoreCode ?? string.Empty,
                    premise.Name,
                    ((premise.Address1 ?? string.Empty) + " "
                        + (premise.Address2 ?? string.Empty) + " "
                        + (premise.Address3 ?? string.Empty)).Trim(),
                    premise.City ?? string.Empty,
                    premise.State,
                    (from country in db.Countries
                     where country.Id == premise.CountryId
                     select country.Name).FirstOrDefault() ?? string.Empty,
                    premise.Telephone,
                    premise.Email,
                    (from staff in db.Staffs
                     where staff.Id == premise.AreaManagerStaffId
                     select staff.Name).FirstOrDefault() ?? string.Empty,
                    (from staff in db.Staffs
                     where staff.Id == premise.OperationManagerStaffId
                     select staff.Name).FirstOrDefault() ?? string.Empty,
                    (from brand in db.GeneralData
                     where brand.Id == premise.BrandId
                     select brand.Name).FirstOrDefault() ?? string.Empty,
                    premise.PremiseType))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllPremisesResponse.StoreCode),
                nameof(GetAllPremisesResponse.StoreName),
                nameof(GetAllPremisesResponse.Address),
                nameof(GetAllPremisesResponse.City),
                nameof(GetAllPremisesResponse.Email))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
