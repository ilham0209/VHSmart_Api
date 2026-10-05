using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// The "Halal Information" column of the master list (spec 10.2): Reference No., Authority,
// Expiry Date and Status, all taken from the row's HALAL CERTIFICATE attachment. The certificate
// type is per-company Supporting Document reference data (R-06), not a fixed list, so it is
// recognised by the name Database.md 8 lists for it, matched case-insensitively. The rows are
// read through the same tenant-scoped set as the rest of the list, so a company the material
// was shared WITH sees the row without another company's certificate - the same stance the
// manufacturer and ingredient-status joins take.
internal static class RawMaterialHalalInformation
{
    public const string HalalCertificateDocumentType = "HALAL CERTIFICATE";

    // D-24, the rule the premise tab already uses (PremiseDocumentClock): expiry checks use the
    // Asia/Kuala_Lumpur (UTC+8) local date, never the stored UTC timestamp. Fixed offset - no DST.
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    public static async Task<RawMaterialHalalInfoResponse?> LoadAsync(
        VHSmartDbContext db,
        Guid rawMaterialId,
        CancellationToken ct)
    {
        var rows = await LoadManyAsync(db, [rawMaterialId], ct);
        return rows.GetValueOrDefault(rawMaterialId);
    }

    // One query for a whole page: the list fills the column after paging, never in the sort or
    // the search (the value is derived, not stored - spec 10.2 computes it from the certificate).
    public static async Task<IReadOnlyDictionary<Guid, RawMaterialHalalInfoResponse>> LoadManyAsync(
        VHSmartDbContext db,
        IReadOnlyCollection<Guid> rawMaterialIds,
        CancellationToken ct)
    {
        if (rawMaterialIds.Count == 0)
            return new Dictionary<Guid, RawMaterialHalalInfoResponse>();

        var today = Today();
        var certificates = await db.RawMaterialAttachments
            .AsNoTracking()
            .Where(row => rawMaterialIds.Contains(row.RawMaterialId)
                && row.DocumentType != null
                && row.DocumentType.ForView == SupportingDocumentForView.RawMaterial
                && row.DocumentType.DocumentType.ToUpper() == HalalCertificateDocumentType)
            .ToListAsync(ct);

        return certificates
            .GroupBy(row => row.RawMaterialId)
            // The upload replaces the row for its type, so a live certificate is unique in
            // practice; the ordering only keeps the answer deterministic if one ever is not.
            .ToDictionary(
                group => group.Key,
                group => Project(group.OrderByDescending(row => row.SysDateCreated).First(), today));
    }

    private static RawMaterialHalalInfoResponse Project(RawMaterialAttachmentEntity row, DateOnly today)
    {
        // D-04: null and the 9999-12-31 sentinel both mean "no expiry" - shown empty, never
        // Expired, and therefore Valid.
        var expiryDate = HalalStatusCalculator.ExpiryForDisplay(
            row.ExpiryDate is null ? null : DateOnly.FromDateTime(row.ExpiryDate.Value));

        return new RawMaterialHalalInfoResponse(
            row.ReferenceNo,
            row.Authority,
            expiryDate,
            HalalStatusCalculator.Status(expiryDate, today));
    }
}

public record RawMaterialHalalInfoResponse(
    string? ReferenceNo,
    string? Authority,
    DateOnly? ExpiryDate,
    HalalStatus Status);
