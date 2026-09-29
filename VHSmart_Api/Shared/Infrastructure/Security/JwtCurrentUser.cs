using System.Security.Claims;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Identity comes from the JWT claims of the current request only, never from the body or the
// URL (D-29, spec 21.4 legacy defect). No HTTP context (seed data, background jobs) or an
// unauthenticated request means no user: empty values fail closed and the DbContext stamps
// "system".
public sealed class JwtCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string UserId =>
        AuthenticatedClaim(JwtClaims.UserId) ?? AuthenticatedClaim(ClaimTypes.NameIdentifier) ?? string.Empty;

    public Guid CompanyId => ParseGuidClaim(JwtClaims.CompanyId);

    public Guid RoleId => ParseGuidClaim(JwtClaims.RoleId);

    public bool IsPlatformAdmin => IsTrue(JwtClaims.IsPlatformAdmin);

    public bool ViewAllCompanies => IsTrue(JwtClaims.ViewAllCompanies);

    private Guid ParseGuidClaim(string type) =>
        Guid.TryParse(AuthenticatedClaim(type), out var value) ? value : Guid.Empty;

    private bool IsTrue(string type) =>
        string.Equals(AuthenticatedClaim(type), bool.TrueString, StringComparison.OrdinalIgnoreCase);

    private string? AuthenticatedClaim(string type)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
            return null;

        return user.FindFirst(type)?.Value;
    }
}
