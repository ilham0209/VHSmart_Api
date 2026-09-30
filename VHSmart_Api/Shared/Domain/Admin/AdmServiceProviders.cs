namespace VHSmart_Api.Shared.Domain.Admin;

// Service Provider (spec 5.3 "Manage Services Provider"): a payee reference record kept PER
// COMPANY [T] - each company's admin maintains its own suppliers (spec 3.3, Database.md 3).
// It is the Payee of a Payment > Other row (spec 15, PayPayments.ServiceProviderId); that
// "delete only when unused" guard belongs to PY-02, which owns the referencing table.
// Not seeded; rows are created through the API.
public class ServiceProviderEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // One multi-line address on the spec form - there is no separate City column (spec 5.3).
    public string Address { get; set; } = string.Empty;

    public string Postcode { get; set; } = string.Empty;

    public Guid CountryId { get; set; }

    public string State { get; set; } = string.Empty;

    public string Telephone { get; set; } = string.Empty;

    public string? Fax { get; set; }

    public string? Webpage { get; set; }

    public string Email { get; set; } = string.Empty;

    public string ContactPerson { get; set; } = string.Empty;

    public string? BankName { get; set; }

    public string? BankAccountNo { get; set; }
}
