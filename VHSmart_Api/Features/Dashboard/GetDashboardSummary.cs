using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Dashboard;

// Spec 13 "Summary cards" [CONFIRMED]: the six totals, each with its "More info" link
// (frontend). Expiring uses the D-03 threshold (config value Halal:ExpiringDays, 90 when the
// setting is missing); Valid/Expired/no-expiry follow D-04 through HalalStatusCalculator -
// the expiry ranges are evaluated over the loaded dates so the calculator stays the single
// owner of the rule (CodingRules 11), never a hand-written SQL predicate. Certificates and
// certificate items carry the explicit CompanyId match of their lists; raw materials use the
// shared-visibility set of the master list (CodingRules 7.3) so the tile and the screen
// behind its link agree. "Total Certified Products" is NOT defined by the spec [flagged]:
// it counts DISTINCT products that carry a certificate item linked to a halal certificate.
// The raw-material tile reuses RawMaterialHalalInformation - the master list's own halal
// column, latest HALAL CERTIFICATE per material - and counts expiring or expired rows.
public record GetDashboardSummaryQuery : IRequest<DashboardSummaryResponse>;

public record DashboardSummaryResponse(
    int TotalHalalCertificate,
    int TotalExpiringHalalCertificate,
    int TotalExpiredCertificate,
    int TotalCertifiedProducts,
    int TotalRawMaterial,
    int TotalExpiringAndExpiredRawMaterialHalalCertificate);

// D-03: one config value for every "expiring" threshold on the Dashboard (certificates,
// raw materials, typhoid); 90 - the decision's default - when the setting is absent or
// nonsense. Shared by the summary and the typhoid panel.
internal static class DashboardThresholds
{
    public static int ExpiringDays(IConfiguration configuration) =>
        int.TryParse(configuration["Halal:ExpiringDays"], out var days) && days > 0 ? days : 90;
}

public class GetDashboardSummaryHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IConfiguration configuration)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    public async Task<DashboardSummaryResponse> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken ct)
    {
        var today = PremiseDocumentClock.Today();
        var expiringDays = DashboardThresholds.ExpiringDays(configuration);

        var certificateExpiries = await db.HalalCertificates
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .Select(row => row.ExpiryDate)
            .ToListAsync(ct);

        var materialIds = await db.RawMaterials
            .AsNoTracking()
            .Select(row => row.Id)
            .ToListAsync(ct);
        var materialCertificates = await RawMaterialHalalInformation.LoadManyAsync(
            db, materialIds, ct);

        return new DashboardSummaryResponse(
            certificateExpiries.Count,
            certificateExpiries.Count(expiry =>
                HalalStatusCalculator.IsExpiring(expiry, today, expiringDays)),
            certificateExpiries.Count(expiry =>
                HalalStatusCalculator.IsExpired(expiry, today)),
            await db.CertificateItems
                .AsNoTracking()
                .Where(row => row.CompanyId == user.CompanyId
                    && row.ProductId != null
                    && row.HalalCertificateId != null)
                .Select(row => row.ProductId)
                .Distinct()
                .CountAsync(ct),
            materialIds.Count,
            materialCertificates.Values.Count(info =>
                info.Status == HalalStatus.Expired
                || HalalStatusCalculator.IsExpiring(info.ExpiryDate, today, expiringDays)));
    }
}
