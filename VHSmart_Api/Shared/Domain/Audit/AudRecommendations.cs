namespace VHSmart_Api.Shared.Domain.Audit;

// The Recommendation screen (spec 14.3 [CONFIRMED]): a free-text audit recommendation a
// Finding may point to (AU-03 links >= 1 of these per finding). Audit setup data is per
// company - each company's super admin maintains their own (spec 14.0 [CONFIRMED]) - so the
// row is [T] and scoped by the tenant filter. No uniqueness: codes are free text
// ("recoI", "12345", "r3" are all valid, spec 14.3).
public class RecommendationEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RecommendationCode { get; set; } = string.Empty;

    public string? Description { get; set; }
}
