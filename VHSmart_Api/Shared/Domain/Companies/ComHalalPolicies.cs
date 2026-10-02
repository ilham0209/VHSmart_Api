using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// Company > Halal Policy (spec 7.2): one document per scheme per company - the UQ
// (CompanyId, SchemeId) among live rows enforces the observed pattern (Database.md 3,
// spec 7.2 [VERIFY] default "enforce"). PolicyDate and Document are required (Database.md 3);
// the bytes live in IFileStorage (F-06), the row carries only the File column group.
public class HalalPolicyEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid SchemeId { get; set; }

    public SchemeEntity? Scheme { get; set; }

    public DateTime PolicyDate { get; set; }

    public StoredFile Document { get; set; } = new();
}
