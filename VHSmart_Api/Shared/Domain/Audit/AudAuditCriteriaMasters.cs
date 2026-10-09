namespace VHSmart_Api.Shared.Domain.Audit;

// Reusable master values created inline from the modal's green "+" (spec 14.5: "Criteria and
// Sub Criteria are reusable master values created inline" [CONFIRMED / MANUAL]; Database.md
// 11). Kind is the Database.md enum stored as its spec string (CodingRules 11). UQ
// (CompanyId, Kind, Text) among live rows - the same text may exist once as a Criteria and
// once as a Sub Criteria.
public enum AuditCriteriaMasterKind
{
    Criteria,
    SubCriteria
}

public class AuditCriteriaMasterEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public AuditCriteriaMasterKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;
}
