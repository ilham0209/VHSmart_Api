using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Product.ManageProduct;

// The tab's "Download Halal Certificate" link (spec 9.1): the stored file of the linked raw
// material's HALAL CERTIFICATE attachment, streamed inline. Same rules as every download in
// this codebase (CodingRules 10): the permission of viewing the record, 404 for an unknown /
// foreign / mismatched id, 404 when the raw material has no certificate or no stored bytes.
public record GetProductIngredientCertificateQuery(Guid ProductId, Guid IngredientId)
    : IRequest<ProductIngredientCertificateResponse>;

public record ProductIngredientCertificateResponse(Stream Content, string ContentType);

public class GetProductIngredientCertificateHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetProductIngredientCertificateQuery, ProductIngredientCertificateResponse>
{
    public async Task<ProductIngredientCertificateResponse> Handle(
        GetProductIngredientCertificateQuery request,
        CancellationToken ct)
    {
        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.ProductId && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        await ProductIngredientData.EnsureBrandLinkedAsync(db, user, ct);

        var ingredient = await db.ProductIngredients
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.IngredientId && row.ProductId == request.ProductId,
                ct);
        if (ingredient is null)
            throw new NotFoundException("Ingredient not found.");

        var certificate = await db.RawMaterialAttachments
            .AsNoTracking()
            .Where(row => row.RawMaterialId == ingredient.RawMaterialId
                && row.DocumentType != null
                && row.DocumentType.ForView == SupportingDocumentForView.RawMaterial
                && row.DocumentType.DocumentType.ToUpper()
                    == RawMaterialHalalInformation.HalalCertificateDocumentType)
            .OrderByDescending(row => row.SysDateCreated)
            .FirstOrDefaultAsync(ct);

        if (certificate is null || string.IsNullOrWhiteSpace(certificate.Document.StorageKey))
            throw new NotFoundException("No document.");

        var content = await storage.OpenReadAsync(certificate.Document.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(certificate.Document.ContentType)
            ? "application/octet-stream"
            : certificate.Document.ContentType;

        return new ProductIngredientCertificateResponse(content, contentType);
    }
}
