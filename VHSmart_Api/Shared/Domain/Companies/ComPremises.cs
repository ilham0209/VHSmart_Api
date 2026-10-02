using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// Premise > Manage Premise (spec 7.7, "core" scope of PR-01): one row per premise (legacy
// called it a branch). PremiseType is the spec's four values stored as their string
// (CodingRules 11). Required columns follow the Database.md 7 markers (* / _): PremiseType,
// Name, Email, Address1/2, Postcode, CountryId, State, Telephone, Status. UQ (CompanyId,
// Email) and (CompanyId, StoreCode) among live rows (Database.md 7, spec 7.7 [CODE]).
// BrandId/MenuConceptId/TagId are NOT editable from the confirmed spec 7.7 form (Brand and
// Store Code "appear on the list"; Tag/Document Status belong to PR-02) - they keep their
// schema shape for the rows loaded elsewhere.
public enum PremiseType
{
    Factory,
    CentralKitchen,
    ColdRoomAndWarehouse,
    RestaurantsAndCafe
}

public class PremiseEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public PremiseType PremiseType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? StoreCode { get; set; }

    public Guid? BrandId { get; set; }

    public GeneralDataEntity? Brand { get; set; }

    // Database.md 7 links this to PrdMenuConcepts, but that table arrives with the Product
    // tasks (PD-xx) and does not exist in the model yet - no FK is configured until then.
    public Guid? MenuConceptId { get; set; }

    // The spec form asks "Select Premise Manager from All Staff Information?" - Yes picks a
    // staff row, No types a name. Both stay as given (the spec defines no cross rule).
    public Guid? PremiseManagerStaffId { get; set; }

    public StaffEntity? PremiseManager { get; set; }

    public string? PremiseManagerName { get; set; }

    public Guid? AreaManagerStaffId { get; set; }

    public StaffEntity? AreaManager { get; set; }

    public Guid? OperationManagerStaffId { get; set; }

    public StaffEntity? OperationManager { get; set; }

    public string? BusinessRegistrationNo { get; set; }

    public string? GoogleMapLink { get; set; }

    public string Address1 { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string? Address3 { get; set; }

    public string Postcode { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? District { get; set; }

    public Guid CountryId { get; set; }

    public CountryEntity? Country { get; set; }

    public string State { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public string? Fax { get; set; }

    public DateTime? OpeningDate { get; set; }

    public DateTime? ClosingDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? PrayerRoomAvailabilityId { get; set; }

    public GeneralDataEntity? PrayerRoomAvailability { get; set; }

    // Written by the Document Status / Tag flow of PR-02 (spec 7.7 list column), never by
    // the premise form.
    public Guid? TagId { get; set; }

    public GeneralDataEntity? Tag { get; set; }
}
