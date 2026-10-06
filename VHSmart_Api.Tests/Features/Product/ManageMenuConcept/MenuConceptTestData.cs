using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using static VHSmart_Api.Tests.Features.Product.ManageMenu.MenuTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

// Concept fixtures for the Manage Menu Concept tests. Menus, companies, categories and raw
// materials come from the Manage Menu fixtures (PD-04) - this screen only LINKS those rows, so
// a second seeder for the same tables would just drift from the first one. Every test works in
// its own in-memory database, so the shared company ids are safe to reuse.
internal static class MenuConceptTestData
{
    public static async Task<Guid> SeedConceptAsync(
        TestableVHSmartDbContext db,
        string name = "Breakfast Menu",
        Guid? companyId = null,
        string? description = "Rotating morning set")
    {
        var row = new MenuConceptEntity
        {
            CompanyId = companyId ?? CompanyA,
            Name = name,
            Description = description
        };
        db.MenuConcepts.Add(row);
        await db.SaveChangesAsync();

        // Seeding and acting are two different requests in production: the handlers load the
        // concept row themselves, so the children must not sit in the change tracker here.
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public static async Task<Guid> SeedLinkAsync(
        TestableVHSmartDbContext db,
        Guid conceptId,
        Guid menuId,
        Guid? companyId = null,
        string mappingStatus = MenuConceptMenuMappingStatus.Active)
    {
        var row = new MenuConceptMenuEntity
        {
            CompanyId = companyId ?? CompanyA,
            MenuConceptId = conceptId,
            MenuId = menuId,
            MappingStatus = mappingStatus
        };
        db.MenuConceptMenus.Add(row);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public static CreateMenuConceptCommand Command(
        string? name = "Breakfast Menu",
        string? description = "Rotating morning set") =>
        new(name, description);

    // menuIds is passed straight through (null stays null): the validator tests need to send
    // the ABSENT payload of spec 9.3, while every other caller hands in its own list.
    public static UpdateMenuConceptCommand UpdateCommand(
        Guid id,
        string? name = "Breakfast Menu Updated",
        string? description = "Rotating morning set",
        IReadOnlyList<Guid>? menuIds = null) =>
        new(id, name, description, menuIds);
}
