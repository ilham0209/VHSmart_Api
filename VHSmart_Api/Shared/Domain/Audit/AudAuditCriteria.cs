namespace VHSmart_Api.Shared.Domain.Audit;

// One row of the "Manage Audit Criteria" list (spec 14.5, Database.md 11): a Category (a
// General Data value - the section heading of the checklist) combined with a reusable
// Criteria / optional Sub Criteria master, the two sequences that drive the checklist
// numbering (1 PEST CONTROL, 1.1 - spec 14.5 [CONFIRMED - inferred, sequence semantics
// VERIFY]), a potential point (maximum score of the criterion; Database.md default 1),
// and the optional Finding Selection links.
public class AuditCriteriaEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid CategoryId { get; set; }

    public int CategorySequence { get; set; }

    public Guid CriteriaId { get; set; }

    public int CriteriaSequence { get; set; }

    // Optional (Database.md 11 carries no *): the modal's Sub Criteria dropdown may be left
    // empty.
    public Guid? SubCriteriaId { get; set; }

    // The source of Reference Category values is an open question (spec 14.5 [VERIFY],
    // question 36) - stored as the plain text(100) column Database.md 11 defines.
    public string? ReferenceCategory { get; set; }

    public string? Reference { get; set; }

    // Potential Point = maximum score; Database.md 11 default 1 (v4.9.2 shows 1, the manual
    // 0 - spec 14.5 [VERIFY], Database.md resolves it to 1).
    public decimal PotentialPoint { get; set; } = 1m;

    public string? Description { get; set; }
}
