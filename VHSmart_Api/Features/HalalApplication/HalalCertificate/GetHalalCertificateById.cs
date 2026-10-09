using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.HalalCertificate;

// View / Edit detail of one Halal Certificate row (spec 12.8 Action column): the editable
// fields, the uploaded file name (null until step 5 of the manual flow uploads it) and the
// items this certificate covers - the "Certificate No. table" the edit screen shows. Expiry
// Date is returned display-shaped (D-04 sentinel shown as null) so an edit round trip never
// shows 9999-12-31. An unknown or foreign certificate answers 404.
public record GetHalalCertificateByIdQuery(Guid Id)
    : IRequest<HalalCertificateDetailResponse>;

public record HalalCertificateDetailResponse(
    Guid Id,
    Guid ApplicationId,
    string CertificateNumber,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate,
    string? FileName,
    IReadOnlyList<CertificateCoveredItem> Items);

// One covered product / premise of the certificate.
public record CertificateCoveredItem(
    Guid Id,
    string CertificateItem,
    string? Brand);

public class GetHalalCertificateByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetHalalCertificateByIdQuery, HalalCertificateDetailResponse>
{
    public async Task<HalalCertificateDetailResponse> Handle(
        GetHalalCertificateByIdQuery request,
        CancellationToken ct)
    {
        var certificate = await db.HalalCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Halal certificate not found.");

        return await HalalCertificateDetailLoader.LoadAsync(db, certificate, ct);
    }
}

// Shared by the detail, update and upload responses so every answer carries the same shape.
internal static class HalalCertificateDetailLoader
{
    public static async Task<HalalCertificateDetailResponse> LoadAsync(
        VHSmartDbContext db,
        HalalCertificateEntity certificate,
        CancellationToken ct)
    {
        var items = await db.CertificateItems
            .AsNoTracking()
            .Where(row => row.HalalCertificateId == certificate.Id)
            .Select(row => new CertificateCoveredItem(
                row.Id,
                row.ItemName,
                row.BrandId == null
                    ? null
                    : db.GeneralData
                        .Where(brand => brand.Id == row.BrandId)
                        .Select(brand => brand.Name)
                        .FirstOrDefault()))
            .OrderBy(row => row.CertificateItem)
            .ToListAsync(ct);

        return new HalalCertificateDetailResponse(
            certificate.Id,
            certificate.ApplicationId,
            certificate.CertificateNo,
            certificate.IssuedDate,
            HalalStatusCalculator.ExpiryForDisplay(certificate.ExpiryDate),
            string.IsNullOrWhiteSpace(certificate.Document?.StorageKey)
                ? null
                : certificate.Document.FileName,
            items);
    }
}
