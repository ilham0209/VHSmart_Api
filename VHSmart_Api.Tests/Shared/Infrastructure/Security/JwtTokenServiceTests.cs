using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class JwtTokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-0123456789abcdef0123456789abcdef";

    [Fact]
    public void CreateToken_CarriesTheFiveD29ClaimsAndAPassingSignature()
    {
        var service = new JwtTokenService(Config());
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var issued = service.CreateToken(userId, companyId, roleId, isPlatformAdmin: true, viewAllCompanies: false);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token);

        Assert.Equal(userId.ToString(), jwt.Claims.Single(claim => claim.Type == JwtClaims.UserId).Value);
        Assert.Equal(companyId.ToString(), jwt.Claims.Single(claim => claim.Type == JwtClaims.CompanyId).Value);
        Assert.Equal(roleId.ToString(), jwt.Claims.Single(claim => claim.Type == JwtClaims.RoleId).Value);
        Assert.Equal(bool.TrueString, jwt.Claims.Single(claim => claim.Type == JwtClaims.IsPlatformAdmin).Value);
        Assert.Equal(bool.FalseString, jwt.Claims.Single(claim => claim.Type == JwtClaims.ViewAllCompanies).Value);

        // The token must actually satisfy the validation parameters Program.cs configures.
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            issued.Token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "VHSmart",
                ValidateAudience = true,
                ValidAudience = "VHSmart_Api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out _);

        Assert.NotNull(principal.Identity);
        Assert.True(principal.Identity.IsAuthenticated);
    }

    [Fact]
    public void CreateToken_UsesConfiguredExpiryMinutes()
    {
        var service = new JwtTokenService(Config(expiryMinutes: 5));
        var before = DateTime.UtcNow;

        var issued = service.CreateToken(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), false, false);

        Assert.InRange(issued.ExpiresAt, before.AddMinutes(5).AddSeconds(-5), before.AddMinutes(5).AddSeconds(5));

        // The JWT carries exp with second precision; it must match the response's ExpiresAt.
        var validTo = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token).ValidTo;
        Assert.InRange(validTo, issued.ExpiresAt.AddSeconds(-2), issued.ExpiresAt);
    }

    [Fact]
    public void CreateToken_MissingSigningKey_ThrowsInsteadOfIssuingATokenNobodyAccepts()
    {
        var service = new JwtTokenService(Config(signingKey: null));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.CreateToken(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, false));

        Assert.Contains("Jwt:SigningKey", exception.Message);
    }

    private static IConfiguration Config(string? signingKey = SigningKey, int expiryMinutes = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = signingKey,
                ["Jwt:Issuer"] = "VHSmart",
                ["Jwt:Audience"] = "VHSmart_Api",
                ["Jwt:ExpiryMinutes"] = expiryMinutes.ToString()
            })
            .Build();
}
