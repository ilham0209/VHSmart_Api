namespace VHSmart_Api.Shared.Domain.Audit;

// The "Recommendation*" checkbox table of the Finding modal (spec 14.4): a Finding needs
// >= 1 of these links, many allowed ([CONFIRMED / MANUAL]). [T] junction - Database.md 11
// carries CompanyId even on child tables; UQ pair (FindingId, RecommendationId) among live
// rows so un-linking (soft delete) frees the pair for re-linking. Unlinking is a soft delete
// like every other delete (Database.md 1 conventions).
public class FindingRecommendationEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid FindingId { get; set; }

    public Guid RecommendationId { get; set; }
}
