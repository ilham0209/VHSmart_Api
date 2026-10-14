using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Support;

// Support > Feedback (spec 17.2, Database.md 3): what the "More Questions? Submit your
// feedback" card stores. Category is the four spec values, kept as the enum string
// (CodingRules 11). Delivery / routing of the feedback is HOLD - owner (spec 17.2) - so the
// table is written and nothing reads it yet.
public enum FeedbackCategory
{
    Enquiry,
    Complaint,
    Comment,
    Suggestion
}

public class SupFeedbackEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }

    public UserEntity? User { get; set; }

    public FeedbackCategory Category { get; set; }

    public string Details { get; set; } = string.Empty;
}
