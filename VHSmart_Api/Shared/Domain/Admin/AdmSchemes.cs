namespace VHSmart_Api.Shared.Domain.Admin;

// One of the 9 halal schemes of the scheme picker (spec 12.1), seeded (Database.md 14).
// IsFoodPremise drives the batch behaviour of HA-01 (a Food Premise batch links premises, a
// product batch links products); SortOrder is the picker order. Abattoirs has no code in any
// screenshot, so Code stays null (VERIFY default in Database.md 3). Global [G] table.
public class SchemeEntity : BaseClass
{
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFoodPremise { get; set; }

    public int SortOrder { get; set; }
}
