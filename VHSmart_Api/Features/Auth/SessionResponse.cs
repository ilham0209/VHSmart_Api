using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Auth;

// What the shell needs after login and after Switch Company (spec 6.2, 21.4): the token plus
// the identity and company list it must render. Rebuilt from the database on every re-issue,
// never echoed from the request.
public record SessionResponse(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string Name,
    string Email,
    Guid RoleId,
    string RoleName,
    bool IsPlatformAdmin,
    bool MustChangePassword,
    Guid ActiveCompanyId,
    IReadOnlyList<UserCompanyResponse> Companies);

public record UserCompanyResponse(Guid CompanyId, bool IsDefault);

// Shared by Login and SwitchCompany: no feature calls another feature's handler, so the
// session is assembled by one function both handlers use (CodingRules 4).
public static class SessionBuilder
{
    // Login: the active company is the default membership, else the first one (ordered so
    // every API instance issues the same token); a platform admin may have none yet
    // (Guid.Empty, D-07 - super user sees all companies anyway).
    public static async Task<SessionResponse> BuildAsync(
        VHSmartDbContext db,
        IJwtTokenService tokenService,
        UserEntity user,
        CancellationToken cancellationToken)
    {
        var companies = await LoadCompaniesAsync(db, user.Id, cancellationToken);
        var activeCompanyId = companies.Count > 0 ? companies[0].CompanyId : Guid.Empty;
        return await BuildAsync(db, tokenService, user, activeCompanyId, companies, cancellationToken);
    }

    // Switch Company: the target company was already checked against AdmUserCompanies
    // (spec 7.4, D-29), so it becomes the active claim of the re-issued token.
    public static async Task<SessionResponse> BuildAsync(
        VHSmartDbContext db,
        IJwtTokenService tokenService,
        UserEntity user,
        Guid activeCompanyId,
        CancellationToken cancellationToken)
    {
        var companies = await LoadCompaniesAsync(db, user.Id, cancellationToken);
        return await BuildAsync(db, tokenService, user, activeCompanyId, companies, cancellationToken);
    }

    private static async Task<SessionResponse> BuildAsync(
        VHSmartDbContext db,
        IJwtTokenService tokenService,
        UserEntity user,
        Guid activeCompanyId,
        IReadOnlyList<UserCompanyResponse> companies,
        CancellationToken cancellationToken)
    {
        var roleName = await db.Roles.AsNoTracking()
            .Where(role => role.Id == user.RoleId)
            .Select(role => role.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        // viewAllCompanies mirrors the platform-admin flag: spec 3.1 gives the super user
        // "Switch Company = ALL" and AdmUsers has no separate column (D-29 claim list).
        var issued = tokenService.CreateToken(
            user.Id,
            activeCompanyId,
            user.RoleId,
            user.IsPlatformAdmin,
            user.IsPlatformAdmin);

        return new SessionResponse(
            issued.Token,
            issued.ExpiresAt,
            user.Id,
            user.Name,
            user.Email,
            user.RoleId,
            roleName,
            user.IsPlatformAdmin,
            user.MustChangePassword,
            activeCompanyId,
            companies);
    }

    private static async Task<IReadOnlyList<UserCompanyResponse>> LoadCompaniesAsync(
        VHSmartDbContext db,
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.UserCompanies.AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderByDescending(membership => membership.IsDefault)
            .ThenBy(membership => membership.CompanyId)
            .Select(membership => new UserCompanyResponse(membership.CompanyId, membership.IsDefault))
            .ToListAsync(cancellationToken);
}
