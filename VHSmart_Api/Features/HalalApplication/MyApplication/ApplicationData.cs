using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Shared helpers and the Additional Information option catalog for the application screen
// (spec 12.5). One place where "the application is found and may be edited" lives, so every
// handler answers 404 for a foreign row and 422 after submit identically (D-26 read-only).
internal static class ApplicationData
{
    // Unknown or foreign application -> 404, never 403 (CodingRules 9).
    public static async Task<ApplicationEntity> FindAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid id,
        CancellationToken ct)
    {
        var application = await db.Applications.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct);

        return application is null
            ? throw new NotFoundException("Application not found.")
            : application;
    }

    // D-26: "Submitted applications are read-only except status tagging, certificate number
    // and attachments". Nothing but DRAFT can exist until HA-04, so the guard is vacuous
    // today but correct; its message is ours (the spec gives none).
    public static void EnsureDraft(ApplicationEntity application)
    {
        if (application.Status != ApplicationStatus.Draft)
            throw new BusinessRuleException(
                "Submitted applications cannot be edited except for status tagging.");
    }

    // The Additional Information checkbox catalog (spec 12.5): codes follow the Database.md
    // 10 examples (CARTON_BOX, HACCP, MS_ISO). The free-text carriers are the spec's
    // "Others + text", "MS ISO + text" and the cleaning "please explain" - text presence is
    // not required (the spec shows no rule, flagged).
    public static readonly IReadOnlySet<string> PackagingOptions = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "CARTON_BOX", "BOTTLE", "PAPER", "PLASTIC", "ALUMINIUM_FOIL", "OTHERS"
    };

    public static readonly IReadOnlySet<string> QualityControlOptions = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "HACCP", "MS_ISO", "GMP", "GHP", "TQM", "MESTI",
        "NORMAL_CLEANING", "SCHEDULED_CLEANING", "OTHERS"
    };

    public static bool IsKnownOption(ApplicationAdditionalInfoSection section, string optionCode) =>
        section switch
        {
            ApplicationAdditionalInfoSection.Packaging => PackagingOptions.Contains(optionCode),
            ApplicationAdditionalInfoSection.QualityControl =>
                QualityControlOptions.Contains(optionCode),
            _ => false
        };

    // Canonical Database.md 10 casing ("New", "Renewal") whatever casing the picker sent.
    public static string NormalizeApplicationType(string? applicationType) =>
        string.IsNullOrWhiteSpace(applicationType)
            ? "New"
            : string.Equals(applicationType, "Renewal", StringComparison.OrdinalIgnoreCase)
                ? "Renewal"
                : "New";
}
