using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// Manufacturer & Supplier list (spec 10.1 "Show N entries" table): the manufacturer half
// (Name, Address, Email, Phone No), the supplier half (same four) and Category = the
// Manufacturer Type value. A half that the row does not use (Type decides) carries nulls here -
// the client renders them as "N/A" (spec 10.1); the Action column is client-side only.
// This is a [T] table: the global CompanyId filter scopes the rows to the caller's company,
// so no extra company check is needed.
public record GetAllManufacturerSuppliersQuery
    : IRequest<DataGridResponse<GetAllManufacturerSuppliersResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllManufacturerSuppliersResponse(
    Guid Id,
    string? ManufacturerName,
    string? ManufacturerAddress,
    string? ManufacturerEmail,
    string? ManufacturerContactNo,
    string? SupplierName,
    string? SupplierAddress,
    string? SupplierEmail,
    string? SupplierContactNo,
    string? Category,
    DateTime? ModifiedDate);

public class GetAllManufacturerSuppliersHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllManufacturerSuppliersQuery, DataGridResponse<GetAllManufacturerSuppliersResponse>>
{
    public async Task<DataGridResponse<GetAllManufacturerSuppliersResponse>> Handle(
        GetAllManufacturerSuppliersQuery request,
        CancellationToken ct) =>
        await db.ManufacturerSuppliers
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(ManufacturerSupplierEntity.ManufacturerName),
                nameof(ManufacturerSupplierEntity.ManufacturerEmail),
                nameof(ManufacturerSupplierEntity.SupplierName),
                nameof(ManufacturerSupplierEntity.SupplierEmail))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(row => new GetAllManufacturerSuppliersResponse(
                row.Id,
                row.ManufacturerName,
                row.ManufacturerAddress,
                row.ManufacturerEmail,
                row.ManufacturerContactNo,
                row.SupplierName,
                row.SupplierAddress,
                row.SupplierEmail,
                row.SupplierContactNo,
                db.GeneralData
                    .Where(data => data.Id == row.ManufacturerTypeId)
                    .Select(data => data.Name)
                    .FirstOrDefault(),
                row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
}
