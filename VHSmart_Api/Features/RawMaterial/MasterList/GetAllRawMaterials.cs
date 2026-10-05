using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// Raw Material Master List (spec 10.2 "Show N entries" table): Ingredient Code, Ingredient,
// Manufacturer Name, Accessible for, Halal Information, Ingredient Status and Packaging Raw
// Material. One spec column is missing on purpose: Assessment Status belongs to the risk
// assessment (10.3, on hold). The Action column is client-side only.
// Visibility is the special filter of CodingRules 7.3 (owner OR listed in Accessible For OR
// Switch Company = ALL), configured once on the entity - this handler just queries the set.
public record GetAllRawMaterialsQuery : IRequest<DataGridResponse<GetAllRawMaterialsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllRawMaterialsResponse(
    Guid Id,
    string? IngredientCode,
    string? Ingredient,
    string? ManufacturerName,
    IReadOnlyList<string> AccessibleFor,
    RawMaterialHalalInfoResponse? HalalInformation,
    string? IngredientStatus,
    bool IsPackagingMaterial,
    DateTime? ModifiedDate);

public class GetAllRawMaterialsHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllRawMaterialsQuery, DataGridResponse<GetAllRawMaterialsResponse>>
{
    public async Task<DataGridResponse<GetAllRawMaterialsResponse>> Handle(
        GetAllRawMaterialsQuery request,
        CancellationToken ct)
    {
        // The response carries the "Accessible For" list, and EF cannot sort above a collection
        // projection (it would have to order the materialized rows), so the order - the client
        // column, Id breaking its ties - is fixed on the rows themselves, before the mapping.
        var sortBy = string.IsNullOrWhiteSpace(request.Request.SortBy)
            ? nameof(RawMaterialEntity.Id)
            : request.Request.SortBy;

        var grid = await db.RawMaterials
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RawMaterialEntity.Ingredient),
                nameof(RawMaterialEntity.IngredientCode),
                nameof(RawMaterialEntity.CommercialName),
                nameof(RawMaterialEntity.ScientificName))
            .ApplySort(sortBy, request.Request.SortDescending)
            .Select(row => new GetAllRawMaterialsResponse(
                row.Id,
                row.IngredientCode,
                row.Ingredient,
                db.ManufacturerSuppliers
                    .Where(supplier => supplier.Id == row.ManufacturerSupplierId)
                    .Select(supplier => supplier.ManufacturerName)
                    .FirstOrDefault(),
                row.AccessibleCompanies
                    .Where(access => !access.IsDeleted)
                    .Select(access => db.Companies
                        .Where(company => company.Id == access.AccessibleCompanyId)
                        .Select(company => company.Name)
                        .FirstOrDefault() ?? string.Empty)
                    .ToList(),
                // Filled after paging: the HALAL CERTIFICATE of each row of THIS page is
                // derived data (spec 10.2), so it never takes part in the search or the sort.
                null,
                db.GeneralData
                    .Where(data => data.Id == row.IngredientStatusId)
                    .Select(data => data.Name)
                    .FirstOrDefault(),
                row.IsPackagingMaterial,
                row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);

        var halalInformation = await RawMaterialHalalInformation.LoadManyAsync(
            db, [.. grid.Data.Select(row => row.Id)], ct);
        grid.Data = [.. grid.Data.Select(
            row => row with { HalalInformation = halalInformation.GetValueOrDefault(row.Id) })];

        return grid;
    }
}
