using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Shared.Domain.Companies;

// A company (spec 6.3 Manage Companies, spec 7.1 Company > General - Database.md 3). This is
// the TENANT ROOT: the row carries no CompanyId because its Id IS the tenant id every [T]
// table points at, which is also why the global tenant filter must not apply here - the
// platform admin reads every company from this table. Not seeded; rows arrive through the API.
public class CompanyEntity : BaseClass
{
    public string Name { get; set; } = string.Empty;

    public Guid CertificationBodyId { get; set; }

    // Nullable navigation over the required FK: the list shows the CB name, and a soft-deleted
    // CB reads as null instead of hiding the company row.
    public CertificationBodyEntity? CertificationBody { get; set; }

    // D-13: free string columns whose option lists live in code (CompanyOptionsCatalog).
    public string? RegistrationType { get; set; }

    public string BusinessRegistrationNo { get; set; } = string.Empty;

    public string? OwnerStatus { get; set; }

    public string Address1 { get; set; } = string.Empty;

    public string Address2 { get; set; } = string.Empty;

    public string? Address3 { get; set; }

    public string PostCode { get; set; } = string.Empty;

    public string? City { get; set; }

    public string District { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public Guid CountryId { get; set; }

    public string Telephone { get; set; } = string.Empty;

    public string? Fax { get; set; }

    public Guid? SchemeId { get; set; }

    public string? IndustrySize { get; set; }

    public string? WebsiteUrl { get; set; }

    // (= Master User e-mail, Database.md 3.)
    public string Email { get; set; } = string.Empty;

    // Database.md 3: must be strictly earlier than today (enforced in the validators).
    public DateTime? DateOfEstablishment { get; set; }

    public string? MainProductsServices { get; set; }

    public string? Market { get; set; }

    public bool IsActive { get; set; } = true;

    // Spec 6.3 / D-20: the brands a company uses are AdmGeneralData (COMPANY / Brand) rows
    // linked through ComCompanyBrands - several per company are allowed, and a company needs
    // at least one to retrieve product ingredient info (PD-02 gates on that, creation does not).
    public List<CompanyBrandEntity> Brands { get; set; } = [];
}
