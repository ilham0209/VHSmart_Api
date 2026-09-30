namespace VHSmart_Api.Shared.Domain.Calculators;

// One row of the Premise Attachment tab as the calculator needs it: the document type text,
// whether a file is uploaded for it (an untouched row shows "N/A" in the UI) and its expiry.
public sealed record PremiseDocument(string DocumentType, bool HasUpload, DateOnly? ExpiryDate);

// The Document Status column of Manage Premise and Advanced Search (D-15).
public sealed record PremiseDocumentStatus(string Text, bool IsComplete, int ExpiredCount);

// Document Status for a premise (§7.7, D-15). Pure: no DbContext, no clock - the caller passes
// today (D-24: the local date, not the stored UTC timestamp).
public static class PremiseDocumentStatusCalculator
{
    public const string NotCompleteText = "NOT COMPLETE DOCUMENTATION";
    public const string CompleteText = "COMPLETE DOCUMENTATION";

    // D-15: fixed list in code, the owner extends it here (not configurable per company).
    public static readonly IReadOnlyList<string> RequiredDocumentTypes =
    [
        "APPOINTMENT LETTER",
        "BUSINESS LICENSE",
        "COMPANY INFORMATION",
        "FOSIM",
        "HALAL CERTIFICATE"
    ];

    public static string ExpiredText(int expiredCount) =>
        $"{expiredCount} OF THE DOCUMENT HAS EXPIRED";

    public static PremiseDocumentStatus Calculate(
        IReadOnlyCollection<PremiseDocument> documents,
        DateOnly today)
    {
        var expiredCount = 0;

        foreach (var documentType in RequiredDocumentTypes)
        {
            var uploaded = documents
                .Where(document =>
                    string.Equals(document.DocumentType, documentType, StringComparison.OrdinalIgnoreCase)
                    && document.HasUpload)
                .ToList();

            // Any required type without a file (missing row or an untouched "N/A" row) stops
            // there: the premise cannot be Complete and is kept out of the pick-lists (D-15).
            if (uploaded.Count == 0)
                return new PremiseDocumentStatus(NotCompleteText, IsComplete: false, ExpiredCount: 0);

            if (uploaded.Any(document => HalalStatusCalculator.IsExpired(document.ExpiryDate, today)))
                expiredCount++;
        }

        return expiredCount > 0
            ? new PremiseDocumentStatus(ExpiredText(expiredCount), IsComplete: false, ExpiredCount: expiredCount)
            : new PremiseDocumentStatus(CompleteText, IsComplete: true, ExpiredCount: 0);
    }
}
