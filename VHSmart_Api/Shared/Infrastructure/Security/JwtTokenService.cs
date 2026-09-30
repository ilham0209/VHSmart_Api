using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VHSmart_Api.Shared.Infrastructure.Security;

public interface IJwtTokenService
{
    IssuedToken CreateToken(
        Guid userId,
        Guid companyId,
        Guid roleId,
        bool isPlatformAdmin,
        bool viewAllCompanies);
}

public record IssuedToken(string Token, DateTime ExpiresAt);

// Issues the access token of D-29 / CodingRules 8.1: exactly five identity claims, lifetime
// from Jwt:ExpiryMinutes (default 60), signing key from configuration only. A missing key
// fails closed - no token can be issued, and Program.cs already refuses to validate any
// without one, so a silently rejected token is impossible.
public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    private const int DefaultExpiryMinutes = 60;

    public IssuedToken CreateToken(
        Guid userId,
        Guid companyId,
        Guid roleId,
        bool isPlatformAdmin,
        bool viewAllCompanies)
    {
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured, so no token can be issued. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" <key>");

        var expiresAt = DateTime.UtcNow.AddMinutes(ExpiryMinutes());

        var claims = new[]
        {
            new Claim(JwtClaims.UserId, userId.ToString()),
            new Claim(JwtClaims.CompanyId, companyId.ToString()),
            new Claim(JwtClaims.RoleId, roleId.ToString()),
            new Claim(JwtClaims.IsPlatformAdmin, isPlatformAdmin ? bool.TrueString : bool.FalseString),
            new Claim(JwtClaims.ViewAllCompanies, viewAllCompanies ? bool.TrueString : bool.FalseString)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private int ExpiryMinutes() =>
        int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) && minutes > 0
            ? minutes
            : DefaultExpiryMinutes;
}
