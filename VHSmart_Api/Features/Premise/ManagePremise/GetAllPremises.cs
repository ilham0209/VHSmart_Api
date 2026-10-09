using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Premise List (spec 7.7, CONFIRMED columns): Action is client side (only the Id is needed);
// Premise Information (Store Code, Store Name, Address, City, State, Country, Phone No.,
// Email), Representative (Area Manager, Operation Manager), Brand and Premise Type come from
// the row. Document Status (D-15) is computed in memory for the returned page only - one
// extra query over the page's attachments feeding the pure calculator, never a stored column
// (Database.md: "Document Status is computed, not stored"). Tag is the premise's TagId plus
// the Premise Tag General Data name for the Tag link (spec 7.7; null name = "CLICK TO ADD"
// placeholder on the client). The seven Halal Information columns (spec 7.7 [CONFIRMED]) are
// the premise's NEWEST application - the spec shows the columns as single cells while
// Advanced Search (spec 8) shows the full numbered list, so the list takes StatusDate
// descending, ties by Id (ours, flagged) - loaded after paging by PremiseHalalInfoLoader
// with its certificate columns following the exactly-one rule (Q17); a premise never
// associated to a batch leaves them all null. The "Premise Type"
// toolbar filter (ALL / specific) binds as a nullable enum: null = ALL. Search covers the
// visible Premise Information columns; the spec states no default order, so StoreName
// ascending is used (flagged in the report). The explicit CompanyId match keeps a ViewAll
// caller on their own rows (same guard as every other list).
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
    string? VhSmartReferenceNo,
    string? CbReferenceNo,
    string? Scheme,
    string? ApplicationStatus,
    string? HalalCertificateNo,
    HalalStatus? HalalCertificateStatus,
    DateOnly? HalalExpiryDate,
    string AreaManager,
    string OperationManager,
    string Brand,
    PremiseType PremiseType,
    string DocumentStatus,
    Guid? TagId,
    string TagName);

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

        var grid = await (
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
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    (from staff in db.Staffs
                     where staff.Id == premise.AreaManagerStaffId
                     select staff.Name).FirstOrDefault() ?? string.Empty,
                    (from staff in db.Staffs
                     where staff.Id == premise.OperationManagerStaffId
                     select staff.Name).FirstOrDefault() ?? string.Empty,
                    (from brand in db.GeneralData
                     where brand.Id == premise.BrandId
                     select brand.Name).FirstOrDefault() ?? string.Empty,
                    premise.PremiseType,
                    string.Empty,
                    premise.TagId,
                    (from tag in db.GeneralData
                     where tag.Id == premise.TagId
                     select tag.Name).FirstOrDefault() ?? string.Empty))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllPremisesResponse.StoreCode),
                nameof(GetAllPremisesResponse.StoreName),
                nameof(GetAllPremisesResponse.Address),
                nameof(GetAllPremisesResponse.City),
                nameof(GetAllPremisesResponse.Email))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);

        // Document Status is computed over the page (the calculator is pure and needs the
        // whole required-type set per premise), never stored and never filtered on here;
        // the Halal Information columns are the newest application's join, loaded the same
        // way so neither takes part in the search or the sort.
        List<Guid> pageIds = [.. grid.Data.Select(row => row.Id)];
        if (pageIds.Count == 0)
            return grid;

        var halalByPremise = await PremiseHalalInfoLoader.LoadAsync(db, user, pageIds, ct);
        var attachments = await db.PremiseAttachments.AsNoTracking()
            .Where(row => pageIds.Contains(row.PremiseId))
            .ToListAsync(ct);
        var documentsByPremise = attachments
            .Where(row => !string.IsNullOrWhiteSpace(row.Document.StorageKey))
            .GroupBy(row => row.PremiseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<PremiseDocument>)group
                    .Select(row => new PremiseDocument(
                        row.DocumentType,
                        HasUpload: true,
                        HalalStatusCalculator.ExpiryForDisplay(
                            row.ExpiryDate is null ? null : DateOnly.FromDateTime(row.ExpiryDate.Value))))
                    .ToList());
        var today = PremiseDocumentClock.Today();
        grid.Data = [.. grid.Data.Select(row =>
        {
            var latestApplication = halalByPremise.TryGetValue(row.Id, out var applications)
                ? applications.FirstOrDefault()
                : null;
            return row with
            {
                DocumentStatus = PremiseDocumentStatusCalculator.Calculate(
                    documentsByPremise.TryGetValue(row.Id, out var documents) ? documents : [],
                    today).Text,
                VhSmartReferenceNo = latestApplication?.VhSmartReferenceNo,
                CbReferenceNo = latestApplication?.CbReferenceNo,
                Scheme = latestApplication?.Scheme,
                ApplicationStatus = latestApplication?.ApplicationStatus,
                HalalCertificateNo = latestApplication?.HalalCertificateNo,
                HalalCertificateStatus = latestApplication?.HalalCertificateStatus,
                HalalExpiryDate = latestApplication?.HalalExpiryDate
            };
        })];

        return grid;
    }
}
