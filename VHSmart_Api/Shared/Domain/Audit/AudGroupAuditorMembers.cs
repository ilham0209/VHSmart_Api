namespace VHSmart_Api.Shared.Domain.Audit;

// One ticked auditor of a group (spec 14.7 modal auditor table, Database.md 13): the
// member is an AdmUsers row with role Auditor / Chief Auditor in the caller's company
// (D-10 - the [VERIFY] "source list not confirmed" of spec 14.7 resolved by the DEFAULT).
// UQ pair (Database.md 13) - applied filtered on company + live rows like every other
// junction, so an un-tick frees the pair for re-ticking.
public class GroupAuditorMemberEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid GroupAuditorId { get; set; }

    public Guid UserId { get; set; }
}
