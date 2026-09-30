namespace VHSmart_Api.Shared.Domain.Admin;

// Web Links (spec 5.4): per-company reference data [T] that feeds the Reference menu cards
// (spec 17.1 - icon + title, each card opens the URL in a new tab; Database.md 3).
// Icon is required on the spec form (Icon*), so unlike Logo / ProfilePicture it is a non-null
// File column group: the bytes live in IFileStorage (F-06), the row carries only metadata.
// Not seeded; rows are created through the API.
public class WebLinkEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Webpage { get; set; } = string.Empty;

    public string? Description { get; set; }

    public StoredFile Icon { get; set; } = new();
}
