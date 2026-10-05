namespace VHSmart_Api.Shared.Domain.RawMaterial;

// Raw Material Master List (spec 10.2, Database.md 8): Core / Supporting Material, stored as
// its string per Database.md "Enums". A row is owned by one company and can be SHARED with
// others through RawMaterialAccessibleCompanies - the list shows "Accessible for" and the
// visibility filter (CodingRules 7.3) reads that table.
public enum RawMaterialCategory
{
    Core,
    Supporting
}

public class RawMaterialEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public RawMaterialCategory Category { get; set; }

    // AdmGeneralData (PRODUCT / Ingredient Status), required on the spec form.
    public Guid IngredientStatusId { get; set; }

    public string? Ingredient { get; set; }

    public string? IngredientCode { get; set; }

    public string? CommercialName { get; set; }

    public string? ScientificName { get; set; }

    // AdmGeneralData (PRODUCT / Ingredient Source), required on the spec form.
    public Guid? IngredientSourceId { get; set; }

    public Guid ManufacturerSupplierId { get; set; }

    public bool IsPackagingMaterial { get; set; }

    // "Accessible For" (spec 10.2): the companies that may see this material. Required (>= 1
    // row) - enforced in the handlers, this navigation feeds the visibility filter.
    public ICollection<RawMaterialAccessibleCompanyEntity> AccessibleCompanies { get; set; } =
        new List<RawMaterialAccessibleCompanyEntity>();
}

// Not a [T] table (Database.md 8 lists no CompanyId): the row belongs to the raw material it
// shares, and the shared-with company is the AccessibleCompanyId column.
public class RawMaterialAccessibleCompanyEntity : BaseClass
{
    public Guid RawMaterialId { get; set; }

    public Guid AccessibleCompanyId { get; set; }
}
