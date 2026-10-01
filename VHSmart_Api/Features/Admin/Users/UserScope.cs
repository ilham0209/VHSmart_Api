using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// Data scope for the Manage Users form (spec 3.3, D-07): the platform admin may link any
// company and assign any role; a company admin only their own company and only the system
// roles (CompanyId null - Database.md 5). Anything outside that scope answers 404 "not
// found", never 403, so an id from another tenant cannot be probed (CodingRules 9).
public static class UserScope
{
    public static async Task<IReadOnlyList<Guid>> ResolveCompanyIdsAsync(
        VHSmartDbContext db,
        ICurrentUser caller,
        IReadOnlyList<Guid> requested,
        CancellationToken ct)
    {
        var wanted = requested.Distinct().ToList();
        if (wanted.Count == 0)
            return wanted;

        var live = await db.Companies.AsNoTracking()
            .Where(company => wanted.Contains(company.Id))
            .Select(company => company.Id)
            .ToListAsync(ct);
        var liveSet = live.ToHashSet();

        var assignableSet = caller.IsPlatformAdmin
            ? liveSet
            : liveSet.Where(id => id == caller.CompanyId).ToHashSet();

        // Any requested company that is missing or outside the caller's scope is a 404, so an
        // id from another tenant cannot be told apart from a typo (CodingRules 9). The
        // surviving ids keep the request order: the first one becomes the default membership.
        if (wanted.Any(id => !assignableSet.Contains(id)))
            throw new NotFoundException("Company not found.");

        return wanted;
    }

    public static async Task<RoleEntity> ResolveRoleAsync(
        VHSmartDbContext db,
        ICurrentUser caller,
        Guid roleId,
        CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == roleId, ct);
        if (role is null)
            throw new NotFoundException("Role not found.");

        if (!caller.IsPlatformAdmin && role.CompanyId is not null)
            throw new NotFoundException("Role not found.");

        return role;
    }
}
