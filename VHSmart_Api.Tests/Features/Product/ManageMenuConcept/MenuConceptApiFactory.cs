using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Tests.Features.Product.ManageMenu;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

// Real pipeline of the Manage Menu tests with the permission stub granting the
// Product.ManageMenuConcept screen instead (PD-05): same JWT minting, same in-memory store,
// same company/menu seeders - this screen never owns a menu row, it only links them, so the
// menu fixtures stay where they were written.
internal sealed class MenuConceptApiFactory : MenuApiFactory
{
    public MenuConceptApiFactory(Guid grantedRoleId, bool grantPermissions = true)
        : base(grantedRoleId, grantPermissions)
    {
    }

    protected override string PermissionKey => PermissionKeys.ProductManageMenuConcept;

    // A concept of this factory's company (or of the given one) with no menu linked yet -
    // spec 9.3 saves the concept first and its "List of Menu" second.
    public async Task<Guid> SeedMenuConceptAsync(
        string name = "Breakfast Menu",
        Guid? companyId = null,
        string? description = "Rotating morning set")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new MenuConceptEntity
        {
            CompanyId = companyId ?? CompanyId,
            Name = name,
            Description = description
        };
        db.MenuConcepts.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // One pair of the concept's "List of Menu" (Database.md 9: one live row per pair).
    public async Task<Guid> SeedMenuConceptLinkAsync(
        Guid menuConceptId,
        Guid menuId,
        Guid? companyId = null,
        string mappingStatus = MenuConceptMenuMappingStatus.Active)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var row = new MenuConceptMenuEntity
        {
            CompanyId = companyId ?? CompanyId,
            MenuConceptId = menuConceptId,
            MenuId = menuId,
            MappingStatus = mappingStatus
        };
        db.MenuConceptMenus.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
