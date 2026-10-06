namespace VHSmart_Api.Shared.Domain.Product;

// Manage Menu (spec 9.2, Database.md 9 "PrdMenus"): the menu of one company, its "List of
// Company" (Accessible For) and its "List of Raw Materials". A row is owned by one company and
// can be SHARED with others through PrdMenuAccessibleCompanies - the list shows "List of
// Company" and the visibility filter (CodingRules 7.3) reads that table.
// The spec prints ACTIVE as the sample value and the Database.md column is a 20-char string
// with no second value; the modal has no Status field either, so a menu is ACTIVE from the
// moment it is created and nothing in this task ever changes it.
public static class MenuStatus
{
    public const string Active = "ACTIVE";
}

public class MenuEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    // Required on the spec form (Menu Name*), unique per company + category (Database.md 9).
    public string? Name { get; set; }

    // AdmGeneralData (COMPANY / Menu Category), required on the spec form (Menu Category*).
    public Guid CategoryId { get; set; }

    // "Start Date" / "End Date" of the modal; together they are the list's Validity Date and
    // "Permanent" is simply the category name of a menu without an end date (9.2, VERIFY).
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = MenuStatus.Active;

    // "Accessible For*" (spec 9.2): the companies that may see this menu. Required (>= 1 row) -
    // enforced in the handlers, this navigation feeds the visibility filter.
    public ICollection<MenuAccessibleCompanyEntity> AccessibleCompanies { get; set; } =
        new List<MenuAccessibleCompanyEntity>();

    // "List of Raw Materials*" (spec 9.2): required (>= 1 row), enforced in the handlers.
    public ICollection<MenuRawMaterialEntity> RawMaterials { get; set; } =
        new List<MenuRawMaterialEntity>();
}

// Not a [T] table (Database.md 9 lists no CompanyId): the row belongs to the menu it shares,
// and the shared-with company is the AccessibleCompanyId column - the Raw Material shape.
public class MenuAccessibleCompanyEntity : BaseClass
{
    public Guid MenuId { get; set; }

    public Guid AccessibleCompanyId { get; set; }
}

// The link table of "List of Raw Materials" [T] (Database.md 9): the menu's own company owns
// the row, the pair is unique among live rows, and unlinking on save is a soft delete like
// everywhere else in this codebase (so the filtered unique index is what keeps it unique).
public class MenuRawMaterialEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid MenuId { get; set; }

    public Guid RawMaterialId { get; set; }
}
