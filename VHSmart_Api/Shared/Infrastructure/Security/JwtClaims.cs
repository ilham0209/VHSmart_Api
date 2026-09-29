namespace VHSmart_Api.Shared.Infrastructure.Security;

// Claim names carried by the tokens we issue (D-29). Single place so the token issuer and
// ICurrentUser cannot drift apart.
public static class JwtClaims
{
    // "sub" is renamed to ClaimTypes.NameIdentifier by the default inbound claim mapping;
    // JwtCurrentUser reads both so either mapping mode works.
    public const string UserId = "sub";

    public const string CompanyId = "companyId";

    public const string RoleId = "roleId";

    public const string IsPlatformAdmin = "isPlatformAdmin";

    public const string ViewAllCompanies = "viewAllCompanies";
}
