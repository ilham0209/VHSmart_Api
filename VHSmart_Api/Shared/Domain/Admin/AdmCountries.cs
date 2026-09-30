namespace VHSmart_Api.Shared.Domain.Admin;

// A country of the shared dropdowns (Database.md 3): seeded with the full ISO 3166-1 list
// (Database.md 14) and read-only through the lookup endpoints (R-01). Global [G] table, so it is
// deliberately NOT an ITenantEntity.
public class CountryEntity : BaseClass
{
    public string Name { get; set; } = string.Empty;

    public string IsoCode { get; set; } = string.Empty;
}
