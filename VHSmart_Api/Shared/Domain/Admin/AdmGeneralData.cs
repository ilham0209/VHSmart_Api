namespace VHSmart_Api.Shared.Domain.Admin;

// The 7 Groups of the General Data screen (spec 5.1 [CONFIRMED]). The members are the spec's
// uppercase values verbatim, so HasConversion<string>() stores "COMPANY" in the nvarchar(30)
// column and JsonStringEnumConverter sends the same value to the frontend (Database.md 3,
// CodingRules 11).
public enum GeneralDataGroup
{
    COMPANY,
    PEOPLE,
    PRODUCT,
    CERTIFICATES,
    PAYMENT,
    AUDIT,
    TRAINING
}

// Dropdown values used across the system (spec 5.1): one row per value, kept PER COMPANY - each
// company's super admin maintains their own reference data (spec 3.3, [CONFIRMED]). Not seeded
// (Database.md 14). The Group -> Category pairs that may exist live in code
// (GeneralDataCatalog); this table only holds the values a company created.
public class GeneralDataEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public GeneralDataGroup Group { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
