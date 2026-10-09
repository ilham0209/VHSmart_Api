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
    // and attachments". Every save endpoint and Submit itself call it; its message is ours
    // (the spec gives none).
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

    // D-26: Submit moves Draft -> "PROCESSING AT {CB} (NEW)" (spec 12.7 [MANUAL] - the CB
    // name may vary by company's CB). The stored status strings are upper case, so the CB
    // name is upper-cased too; it is cut so the string always fits the nvarchar(60) Status
    // column (14 + 40 + 6 = 60). A company whose CB row is missing (data hole) answers
    // "PROCESSING AT CB (NEW)" - the same empty-render fallback the detail screen takes.
    public static string ProcessingStatus(string? certificationBodyName)
    {
        const string prefix = "PROCESSING AT ";
        const string suffix = " (NEW)";
        const int columnLength = 60;
        var cb = string.IsNullOrWhiteSpace(certificationBodyName)
            ? "CB"
            : certificationBodyName.ToUpperInvariant();
        var available = columnLength - prefix.Length - suffix.Length;
        if (cb.Length > available)
            cb = cb[..available];
        return $"{prefix}{cb}{suffix}";
    }

    // D-26 / spec 12.7: the four CB-progress statuses the "Tagging Application Status"
    // dialog may set, in their forward order (the strings of ApplicationStatus verbatim).
    public static readonly IReadOnlyList<string> TaggingStatuses =
    [
        ApplicationStatus.ApplicationApproved,
        ApplicationStatus.AuditInProgress,
        ApplicationStatus.AuditCompleted,
        ApplicationStatus.ApprovedWithDocument
    ];

    // The canonical spelling of a tagging target whatever casing the dialog sent, or null
    // when the value is not one of the four (the validator already says so; the handler
    // never trusts the casing).
    public static string? CanonicalTaggingStatus(string? status)
    {
        foreach (var candidate in TaggingStatuses)
        {
            if (string.Equals(candidate, status, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    // Position of a status in the submit/tagging sequence: the processing status is 0, the
    // four tagging statuses are 1..4 in order. Anything else (DRAFT, a legacy status such as
    // "Payment Confirmed", a foreign spelling) answers -1 and is not taggable - D-26 names
    // only this set. The processing status matches by its D-26 shape because its middle
    // carries the CB name and differs per company.
    public static int TaggingSequenceIndex(string status)
    {
        if (IsProcessingStatus(status))
            return 0;

        for (var i = 0; i < TaggingStatuses.Count; i++)
        {
            if (TaggingStatuses[i] == status)
                return i + 1;
        }

        return -1;
    }

    public static bool IsProcessingStatus(string status) =>
        status.StartsWith("PROCESSING AT ", StringComparison.Ordinal)
        && status.EndsWith(" (NEW)", StringComparison.Ordinal);
}
