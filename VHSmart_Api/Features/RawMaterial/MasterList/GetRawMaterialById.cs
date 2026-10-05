using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// The view/edit form of one raw material (spec 10.2 "Manage Master Raw Material"): every field
// of the form plus the manufacturer name the row carries and the "List of Company" table of the
// Accessible For picker. The halal values are NOT here: the list's Halal Information column and
// the modal's section "Attachment Information" are served by the attachment features next to
// this file (GET {id:guid}/attachments), and Assessment Status stays out with 10.3 on hold. The
// visibility filter (CodingRules 7.3) makes another company's row answer 404 unless it was
// shared with the caller. Shared by Get / Create / Update like ServiceProviderResponse is.
public record GetRawMaterialByIdQuery(Guid Id) : IRequest<RawMaterialResponse>;

public record RawMaterialAccessibleCompanyResponse(Guid CompanyId, string CompanyName);

public record RawMaterialResponse(
    Guid Id,
    RawMaterialCategory Category,
    Guid IngredientStatusId,
    string? Ingredient,
    string? IngredientCode,
    string? CommercialName,
    string? ScientificName,
    Guid? IngredientSourceId,
    Guid ManufacturerSupplierId,
    string? ManufacturerName,
    bool IsPackagingMaterial,
    IReadOnlyList<RawMaterialAccessibleCompanyResponse> AccessibleFor,
    DateTime? ModifiedDate);

public class GetRawMaterialByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetRawMaterialByIdQuery, RawMaterialResponse>
{
    public async Task<RawMaterialResponse> Handle(
        GetRawMaterialByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.RawMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Raw material not found.");

        return await RawMaterialResponseData.From(db, entity, ct);
    }
}

// The company names are read here rather than through a navigation so the detail, create and
// update answers are built by exactly one code path.
internal static class RawMaterialResponseData
{
    public static async Task<RawMaterialResponse> From(
        VHSmartDbContext db,
        RawMaterialEntity entity,
        CancellationToken ct)
    {
        var accessibleFor = await db.RawMaterialAccessibleCompanies
            .AsNoTracking()
            .Where(row => row.RawMaterialId == entity.Id)
            .Select(row => new
            {
                row.AccessibleCompanyId,
                // A company row that has gone (soft deleted) still keeps its id on the sharing
                // row; it renders as an empty cell instead of silently losing the entry.
                CompanyName = db.Companies
                    .Where(company => company.Id == row.AccessibleCompanyId)
                    .Select(company => company.Name)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(ct);

        var manufacturerName = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => row.Id == entity.ManufacturerSupplierId)
            .Select(row => row.ManufacturerName)
            .FirstOrDefaultAsync(ct);

        return new RawMaterialResponse(
            entity.Id,
            entity.Category,
            entity.IngredientStatusId,
            entity.Ingredient,
            entity.IngredientCode,
            entity.CommercialName,
            entity.ScientificName,
            entity.IngredientSourceId,
            entity.ManufacturerSupplierId,
            manufacturerName,
            entity.IsPackagingMaterial,
            [.. accessibleFor
                .OrderBy(row => row.CompanyName, StringComparer.Ordinal)
                .Select(row => new RawMaterialAccessibleCompanyResponse(
                    row.AccessibleCompanyId, row.CompanyName))],
            entity.SysDateModified);
    }
}
