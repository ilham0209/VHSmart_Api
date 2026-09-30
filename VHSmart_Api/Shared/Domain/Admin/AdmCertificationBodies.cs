namespace VHSmart_Api.Shared.Domain.Admin;

// Certification Bodies (spec 5.2, Database.md 3): a GLOBAL reference table - the platform
// admin (Serunai Super User) manages it, there is no CompanyId and no tenant filter (D-07).
// Not seeded; rows are created through the API.
public class CertificationBodyEntity : BaseClass
{
    public string Name { get; set; } = string.Empty;

    public string? Acronym { get; set; }

    public string? Notes { get; set; }

    // The country the CB represents (the form's "Representing Country" dropdown) - optional,
    // distinct from the address Country below.
    public Guid? RepresentingCountryId { get; set; }

    public string? Address1 { get; set; }

    public string? Address2 { get; set; }

    public string? City { get; set; }

    public string? Postcode { get; set; }

    public Guid CountryId { get; set; }

    public string? State { get; set; }

    public string? Telephone { get; set; }

    public string? Fax { get; set; }

    public string? Webpage { get; set; }

    public string? Email { get; set; }

    public string? ContactPerson { get; set; }

    public string? BankName { get; set; }

    public string? BankAccountNo { get; set; }

    // File column group (Database.md 3); bytes live in IFileStorage (F-06), the row only
    // carries the metadata. Nullable: a CB may have no logo.
    public StoredFile? Logo { get; set; }
}
