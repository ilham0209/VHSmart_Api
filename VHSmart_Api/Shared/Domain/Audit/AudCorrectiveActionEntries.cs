using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Audit;

// Who filled the entry (Database.md 13 "Corrective Action History"): the auditee saves
// the corrective action, the auditor reviews it (spec 14.11).
public enum CorrectiveActionFilledBy
{
    Auditee,
    Auditor
}

// One append-only "Corrective Action History" row (Database.md 13, spec 14.11): every
// save by either side keeps a versioned entry. Details carries the auditee's block, Comment
// + StatusSet the auditor's; Attachment is the auditee's file (the auditor block has none).
// Version counts up per non-conformance across both sides.
public class CorrectiveActionEntryEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid NonConformanceId { get; set; }

    public CorrectiveActionFilledBy FilledBy { get; set; }

    // Auditee block (spec 14.11 "Fill by Auditee").
    public string? Details { get; set; }

    // Auditor block (spec 14.11 "Fill by Auditor").
    public string? Comment { get; set; }

    // D-05 status text the auditor set on this entry.
    public string? StatusSet { get; set; }

    public int Version { get; set; }

    public StoredFile? Attachment { get; set; }

    public Guid FilledByUserId { get; set; }

    public UserEntity? FilledByUser { get; set; }
}
