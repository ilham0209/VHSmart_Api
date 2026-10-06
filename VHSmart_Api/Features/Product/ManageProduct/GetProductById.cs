using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The view/edit form of one product (spec 9.1 "Manage Product" - Common Product Information):
// every field of the form plus the manufacturer name the row carries and the QR Code panel's
// key (null = the client's "QR Code yet to be generated" text - QR generation is a paid flow
// this task does not build). SchemeSpecificData is not returned: it stays null until the
// scheme-specific fields are seen, and VerifyHalalPublishStatus belongs to PD-06. The two
// tabs that appear after saving (Ingredient Information, Attachment Information) are PD-02 /
// PD-03. Shared by Get / Create / Update like RawMaterialResponse is.
public record GetProductByIdQuery(Guid Id) : IRequest<ProductResponse>;

public record ProductResponse(
    Guid Id,
    Guid SchemeId,
    string? Name,
    Guid? ManufacturerSupplierId,
    string? ManufacturerName,
    Guid BrandId,
    Guid CategoryId,
    string? Code,
    string? Gtin,
    string? NutritionContentClaims,
    string? PotentialAllergens,
    string? CalorieContent,
    string? AvailableAt,
    string? PackagingSize,
    Guid? MarketingMethodId,
    string? QrCodeKey,
    DateTime? ModifiedDate);

public class GetProductByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetProductByIdQuery, ProductResponse>
{
    public async Task<ProductResponse> Handle(
        GetProductByIdQuery request,
        CancellationToken ct)
    {
        // The explicit CompanyId match keeps a Switch Company = ALL caller on their own rows
        // (same guard as the premise and raw material detail).
        var entity = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Product not found.");

        return await ProductResponseData.From(db, entity, ct);
    }
}

// The manufacturer name is read here rather than through a navigation so the detail, create
// and update answers are built by exactly one code path (mirrors RawMaterialResponseData).
internal static class ProductResponseData
{
    public static async Task<ProductResponse> From(
        VHSmartDbContext db,
        ProductEntity entity,
        CancellationToken ct)
    {
        var manufacturer = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => row.Id == entity.ManufacturerSupplierId)
            .Select(row => new { row.ManufacturerName, row.SupplierName })
            .FirstOrDefaultAsync(ct);

        var manufacturerName = manufacturer is null
            ? null
            : manufacturer.ManufacturerName ?? manufacturer.SupplierName;

        return new ProductResponse(
            entity.Id,
            entity.SchemeId,
            entity.Name,
            entity.ManufacturerSupplierId,
            manufacturerName,
            entity.BrandId,
            entity.CategoryId,
            entity.Code,
            entity.Gtin,
            entity.NutritionContentClaims,
            entity.PotentialAllergens,
            entity.CalorieContent,
            entity.AvailableAt,
            entity.PackagingSize,
            entity.MarketingMethodId,
            entity.QrCodeKey,
            entity.SysDateModified);
    }
}
