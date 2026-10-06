namespace VHSmart_Api.Shared.Domain.Product;

// Manage Menu Concept (spec 9.3, Database.md 9 "PrdMenuConcepts"): a named concept of one
// company ("For Company*" = the CompanyId column) and the menus it groups. Premises point at a
// concept through ComPremises.MenuConceptId - that side is written by the premise task.
// There is no "Accessible For" table here, so the ordinary tenant filter is the whole
// visibility rule and the list's Company Name column is the owner of the row.
public class MenuConceptEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    // "Menu Concept*" of the 9.3 modal - required by the form while Database.md 9 keeps the
    // column nullable, exactly like the other form-required columns of this module.
    public string? Name { get; set; }

    public string? Description { get; set; }

    // "List of Menu" (spec 9.3): linked through PrdMenuConceptMenus, one live row per pair.
    public ICollection<MenuConceptMenuEntity> Menus { get; set; } =
        new List<MenuConceptMenuEntity>();
}

// Database.md 9 gives MappingStatus 20 chars and prints ACTIVE as the only sample. The spec
// shows a Link button per row, so a pair the concept stops listing keeps its row and flips to
// INACTIVE - the PD-02 link/unlink stance - instead of being deleted: the pair stays on the
// table with a status the client can render, and a later Save just flips it back.
public static class MenuConceptMenuMappingStatus
{
    public const string Active = "ACTIVE";

    public const string Inactive = "INACTIVE";
}

// Link table of "List of Menu" [T] (Database.md 9): the concept's own company owns the row,
// the pair is unique among live rows, and the row is never physically removed.
public class MenuConceptMenuEntity : BaseClass, ITenantEntity
{
    public Guid CompanyId { get; set; }

    public Guid MenuConceptId { get; set; }

    public Guid MenuId { get; set; }

    public string MappingStatus { get; set; } = MenuConceptMenuMappingStatus.Active;
}
