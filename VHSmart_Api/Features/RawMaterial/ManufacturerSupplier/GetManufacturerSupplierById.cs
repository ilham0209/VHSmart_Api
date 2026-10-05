using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// The view/edit form of one Manufacturer & Supplier (spec 10.1 "Manufacturer & Supplier
// Information"): every field of both halves plus the Manufacturer Type id the Category dropdown
// needs. Logo travels through its own endpoints (like the CB logo), so only its file name is
// echoed here. The tenant filter answers 404 for another company's row. Shared by
// Get / Create / Update like ServiceProviderResponse is.
public record GetManufacturerSupplierByIdQuery(Guid Id) : IRequest<ManufacturerSupplierResponse>;

public record ManufacturerSupplierResponse(
    Guid Id,
    ManufacturerSupplierType Type,
    string? ManufacturerName,
    string? ManufacturerBusinessRegNo,
    Guid? ManufacturerTypeId,
    string? ManufacturerAddress,
    Guid? ManufacturerCountryId,
    string? ManufacturerPersonInCharge,
    string? ManufacturerContactNo,
    string? ManufacturerEmail,
    string? ManufacturerWebpage,
    string? SupplierName,
    string? SupplierAddress,
    Guid? SupplierCountryId,
    string? SupplierPersonInCharge,
    string? SupplierContactNo,
    string? SupplierEmail,
    string? LogoFileName,
    DateTime? ModifiedDate)
{
    internal static ManufacturerSupplierResponse From(ManufacturerSupplierEntity entity) =>
        new(
            entity.Id,
            entity.Type,
            entity.ManufacturerName,
            entity.ManufacturerBusinessRegNo,
            entity.ManufacturerTypeId,
            entity.ManufacturerAddress,
            entity.ManufacturerCountryId,
            entity.ManufacturerPersonInCharge,
            entity.ManufacturerContactNo,
            entity.ManufacturerEmail,
            entity.ManufacturerWebpage,
            entity.SupplierName,
            entity.SupplierAddress,
            entity.SupplierCountryId,
            entity.SupplierPersonInCharge,
            entity.SupplierContactNo,
            entity.SupplierEmail,
            entity.Logo?.FileName,
            entity.SysDateModified);
}

public class GetManufacturerSupplierByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetManufacturerSupplierByIdQuery, ManufacturerSupplierResponse>
{
    public async Task<ManufacturerSupplierResponse> Handle(
        GetManufacturerSupplierByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.ManufacturerSuppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Manufacturer and supplier not found.");

        return ManufacturerSupplierResponse.From(entity);
    }
}
