namespace VHSmart_Api.Shared.Domain.Companies;

// Personnel > Internal Training (spec 7.5): the training rows of a company. TrainingType is
// the spec's three values stored as their string (CodingRules 11); Name and TrainingDate are
// required (Database.md 6 markers). UQ (CompanyId, Name) among live rows - enforced on create
// and update excluding self per Database.md 6 (legacy checked create only, spec 21.9).
public class TrainingEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public TrainingType TrainingType { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime TrainingDate { get; set; }
}

public enum TrainingType
{
    AllStaff,
    SlaughtermanHalalChecker,
    InternalHalalCommittee
}
