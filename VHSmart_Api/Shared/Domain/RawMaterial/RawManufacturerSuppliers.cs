namespace VHSmart_Api.Shared.Domain.RawMaterial;

// Manufacturer & Supplier (spec 10.1, Database.md 8): one row carries both sides. Type says
// which side is filled - a row can be a manufacturer, a supplier or both, and the other half
// stays null (the list shows "N/A" for an absent side).
public enum ManufacturerSupplierType
{
    ManufacturerOnly,
    SupplierOnly,
    Both
}

public class ManufacturerSupplierEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public ManufacturerSupplierType Type { get; set; }

    public string? ManufacturerName { get; set; }

    public string? ManufacturerBusinessRegNo { get; set; }

    // AdmGeneralData (COMPANY / Manufacturer Type); the list shows its Name as "Category".
    public Guid? ManufacturerTypeId { get; set; }

    public string? ManufacturerAddress { get; set; }

    public Guid? ManufacturerCountryId { get; set; }

    public string? ManufacturerPersonInCharge { get; set; }

    public string? ManufacturerContactNo { get; set; }

    public string? ManufacturerEmail { get; set; }

    public string? ManufacturerWebpage { get; set; }

    public string? SupplierName { get; set; }

    public string? SupplierAddress { get; set; }

    public Guid? SupplierCountryId { get; set; }

    public string? SupplierPersonInCharge { get; set; }

    public string? SupplierContactNo { get; set; }

    public string? SupplierEmail { get; set; }

    // File column group (Database.md 8): optional, bytes live in IFileStorage (F-06).
    public StoredFile? Logo { get; set; }
}
