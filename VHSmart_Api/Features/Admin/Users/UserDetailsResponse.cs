using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// The add/edit form payload of Manage Users (spec 3.2 - the form itself was never
// screenshotted, so these are the fields the spec names plus the columns of Database.md 5).
// Shared by GetById and Update, the way CompanyResponse is shared on the C-01 screen.
public record UserDetailsResponse(
    Guid Id,
    string Name,
    string Email,
    Guid RoleId,
    string? ContactNo,
    bool IsActive,
    DateTime? ActivatedAt,
    IReadOnlyList<Guid> CompanyIds,
    DateTime CreatedDate)
{
    public static async Task<UserDetailsResponse> FromAsync(
        VHSmartDbContext db,
        UserEntity user,
        CancellationToken ct)
    {
        var companyIds = await db.UserCompanies.AsNoTracking()
            .Where(membership => membership.UserId == user.Id)
            .OrderByDescending(membership => membership.IsDefault)
            .ThenBy(membership => membership.CompanyId)
            .Select(membership => membership.CompanyId)
            .ToListAsync(ct);

        return new UserDetailsResponse(
            user.Id,
            user.Name,
            user.Email,
            user.RoleId,
            user.ContactNo,
            user.IsActive,
            user.ActivatedAt,
            companyIds,
            user.SysDateCreated);
    }
}

// Load a user for read/edit/delete with the data scope of spec 3.3: the platform admin sees
// everyone, a company admin only users linked to the active company. A user outside the
// caller's scope is indistinguishable from a missing one - 404, never 403 (CodingRules 9).
public static class UserLookup
{
    public static async Task<UserEntity> GetVisibleUserAsync(
        VHSmartDbContext db,
        ICurrentUser caller,
        Guid userId,
        CancellationToken ct)
    {
        // Global filter already hides soft-deleted rows; login and uniqueness behave the same.
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == userId, ct);
        if (user is null)
            throw new NotFoundException("User not found.");

        if (caller.IsPlatformAdmin)
            return user;

        var linked = await db.UserCompanies.AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == user.Id && membership.CompanyId == caller.CompanyId, ct);
        if (!linked)
            throw new NotFoundException("User not found.");

        return user;
    }
}
