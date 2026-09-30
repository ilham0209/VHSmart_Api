namespace VHSmart_Api.Shared.Domain.Admin;

// A state of a country (Database.md 3). Seeded for Malaysia only (Database.md 14): every other
// country keeps a free-text state on the owning record (spec 7.1). Global [G] table.
public class StateEntity : BaseClass
{
    public Guid CountryId { get; set; }

    public string Name { get; set; } = string.Empty;
}
