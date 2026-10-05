using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// The logo of the view form (spec 10.1 "Logo"): streams the stored bytes inline; a row without
// a logo (or an unknown id) answers 404 - the frontend keeps its own placeholder. Same
// permission as viewing the record (CodingRules 10).
public record GetManufacturerSupplierLogoQuery(Guid Id) : IRequest<GetManufacturerSupplierLogoResponse>;

public record GetManufacturerSupplierLogoResponse(Stream Content, string ContentType);

public class GetManufacturerSupplierLogoHandler(
    VHSmartDbContext db,
    IFileStorage storage)
    : IRequestHandler<GetManufacturerSupplierLogoQuery, GetManufacturerSupplierLogoResponse>
{
    public async Task<GetManufacturerSupplierLogoResponse> Handle(
        GetManufacturerSupplierLogoQuery request,
        CancellationToken ct)
    {
        var entity = await db.ManufacturerSuppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Manufacturer and supplier not found.");

        var logo = entity.Logo
            ?? throw new NotFoundException("No logo.");

        var content = await storage.OpenReadAsync(logo.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(logo.ContentType)
            ? "application/octet-stream"
            : logo.ContentType;

        return new GetManufacturerSupplierLogoResponse(content, contentType);
    }
}
