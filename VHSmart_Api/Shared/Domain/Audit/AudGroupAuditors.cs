namespace VHSmart_Api.Shared.Domain.Audit;

// One group of auditors (spec 14.7, Database.md 13): a named team with a status, exactly
// one checklist template (the radio of the modal, [CONFIRMED]) and one leader chosen among
// the ticked members (D-10). Only Active groups can be chosen in Audit Planning (spec 14.7
// [CONFIRMED - owner]); the rule is enforced by the planning task, not here.
public enum GroupAuditorStatus
{
    Active,
    Inactive
}

public class GroupAuditorEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public GroupAuditorStatus Status { get; set; } = GroupAuditorStatus.Active;

    // Exactly one template per group (spec 14.7 radio [CONFIRMED]; Database.md 13
    // ChecklistId is required).
    public Guid ChecklistId { get; set; }

    // Must be one of the members (D-10, Database.md 13 "Leader must be among members").
    public Guid LeaderUserId { get; set; }
}
