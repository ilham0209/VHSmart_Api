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

// "Download Halal Certificate" of a Menu Information tab row (spec 7.7): the stored file of
// the menu raw material's HALAL CERTIFICATE attachment, streamed inline - the same object
// the product ingredient certificate download serves (spec 9.1), reached through the
// premise's own screen. Every id is checked in scope: the premise must be the caller's
// (404 otherwise, never 403), the menu must sit on that premise's MenuConcept with an ACTIVE
// mapping, and the raw material must be linked to the menu - so a guessed id pair from
// another screen gets a 404, not a file.
public record GetPremiseMenuCertificateQuery(
    Guid PremiseId,
    Guid MenuId,
    Guid RawMaterialId) : IRequest<PremiseMenuCertificateResponse>;

public record PremiseMenuCertificateResponse(Stream Content, string ContentType);

public class GetPremiseMenuCertificateHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetPremiseMenuCertificateQuery, PremiseMenuCertificateResponse>
{
    public async Task<PremiseMenuCertificateResponse> Handle(
        GetPremiseMenuCertificateQuery request,
        CancellationToken ct)
    {
        var premise = await db.Premises
            .AsNoTracking()
            .Where(row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId)
            .Select(row => new { row.MenuConceptId })
            .FirstOrDefaultAsync(ct);
        if (premise is null)
            throw new NotFoundException("Premise not found.");
        if (premise.MenuConceptId is null)
            throw new NotFoundException("Menu not found.");

        var menuBelongsToPremise = await db.MenuConceptMenus
            .AsNoTracking()
            .AnyAsync(row => row.MenuConceptId == premise.MenuConceptId.Value
                && row.CompanyId == user.CompanyId
                && row.MenuId == request.MenuId
                && row.MappingStatus == MenuConceptMenuMappingStatus.Active, ct);
        if (!menuBelongsToPremise)
            throw new NotFoundException("Menu not found.");

        var materialIsOnMenu = await db.MenuRawMaterials
            .AsNoTracking()
            .AnyAsync(row => row.MenuId == request.MenuId
                && row.RawMaterialId == request.RawMaterialId, ct);
        if (!materialIsOnMenu)
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

        return new PremiseMenuCertificateResponse(content, contentType);
    }
}
