using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// "Download Halal Certificate" of a Product Information tab row (spec 7.7): the stored file
// of the product ingredient's raw material HALAL CERTIFICATE attachment. Same rules as the
// menu variant - premise 404 first (tenant stance of this controller), then the product
// must belong to the caller's company (products live on the company, Database.md 9), then
// the raw material must be an ACTIVE ingredient of that product, so only id pairs the tabs
// actually render can stream a file. CodingRules 10: permission of viewing the record,
// unknown / foreign / mismatched ids answer 404, missing bytes answer 404.
public record GetPremiseProductCertificateQuery(
    Guid PremiseId,
    Guid ProductId,
    Guid RawMaterialId) : IRequest<PremiseProductCertificateResponse>;

public record PremiseProductCertificateResponse(Stream Content, string ContentType);

public class GetPremiseProductCertificateHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetPremiseProductCertificateQuery, PremiseProductCertificateResponse>
{
    public async Task<PremiseProductCertificateResponse> Handle(
        GetPremiseProductCertificateQuery request,
        CancellationToken ct)
    {
        var premiseExists = await db.Premises
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId, ct);
        if (!premiseExists)
            throw new NotFoundException("Premise not found.");

        var productExists = await db.Products
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.ProductId
                && row.CompanyId == user.CompanyId, ct);
        if (!productExists)
            throw new NotFoundException("Product not found.");

        var materialIsOnProduct = await db.ProductIngredients
            .AsNoTracking()
            .AnyAsync(row => row.ProductId == request.ProductId
                && row.RawMaterialId == request.RawMaterialId
                && row.MappingStatus == ProductIngredientMappingStatus.Active, ct);
        if (!materialIsOnProduct)
            throw new NotFoundException("Ingredient not found.");

        var certificate = await db.RawMaterialAttachments
            .AsNoTracking()
            .Where(row => row.RawMaterialId == request.RawMaterialId
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

        return new PremiseProductCertificateResponse(content, contentType);
    }
}
