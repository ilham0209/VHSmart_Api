namespace VHSmart_Api.Shared.Domain.Companies;

// Company > Profiles (spec 7.3): one row per roster slot of the company profile. The spec
// form holds exactly one Contact Person and one Halal Executive, both chosen from All Staff,
// so the screen keeps at most one live row per kind (Database.md 4 UQ (CompanyId, Kind,
// StaffId) would allow more, but nothing in the spec ever shows a second one).
public enum CompanyContactKind
{
    ContactPerson,
    HalalExecutive
}

public class CompanyContactEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    // Enum stored as its spec string (CodingRules 11), e.g. "HalalExecutive".
    public CompanyContactKind Kind { get; set; }

    public Guid StaffId { get; set; }

    // Nullable navigation over the required FK: a soft-deleted staff member reads as null
    // instead of hiding the profile row, and the response then shows the ids only.
    public StaffEntity? Staff { get; set; }

    // Working hours are shown next to the person on the Halal application's Company
    // Information tab (spec 12.5) and maintained here; legacy stores the start as HH:mm.
    public TimeOnly? WorkingHourFrom { get; set; }

    public TimeOnly? WorkingHourTo { get; set; }
}
