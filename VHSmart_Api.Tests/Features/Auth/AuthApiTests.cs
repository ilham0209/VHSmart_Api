using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VHSmart_Api.Features.Auth;
using VHSmart_Api.Features.Auth.Login;

namespace VHSmart_Api.Tests.Features.Auth;

public class AuthApiTests
{
    private const string LoginRoute = "/api/auth/login";

    private const string SwitchRoute = "/api/auth/switch-company";

    private const string Password = "Passw0rd!";

    [Fact]
    public async Task Login_MissingCredentials_Returns400WithFieldErrors()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginRoute, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        var errors = problem!.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithGenericDetail()
    {
        using var factory = new AuthApiFactory();
        await factory.SeedUserAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginRoute, new { email = "admin@example.com", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.Equal(
            LoginMessages.InvalidCredentials,
            problem!.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSessionWithAUsableToken()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginRoute, new { email = "admin@example.com", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(user.Id, session.UserId);
        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.Equal("VH Smart Admin", session.RoleName);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        Assert.Equal(
            user.Id.ToString(),
            jwt.Claims.Single(claim => claim.Type == "sub").Value);
    }

    [Fact]
    public async Task Login_LockedAccount_Returns401WithLockMessage()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        await factory.LockAsync(user.Id);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginRoute, new { email = "admin@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.Equal(LoginMessages.Locked, problem!.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_InactiveAccount_Returns403()
    {
        using var factory = new AuthApiFactory();
        await factory.SeedUserAsync(isActive: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginRoute, new { email = "admin@example.com", password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SwitchCompany_WithoutToken_Returns401()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SwitchRoute, new { companyId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwitchCompany_MemberCompany_ReturnsTokenClaimedForThatCompany()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var currentCompany = Guid.NewGuid();
        var targetCompany = Guid.NewGuid();
        await factory.AddMembershipAsync(user.Id, currentCompany, isDefault: true);
        await factory.AddMembershipAsync(user.Id, targetCompany, isDefault: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateTokenFor(user, currentCompany));

        var response = await client.PostAsJsonAsync(SwitchRoute, new { companyId = targetCompany });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(targetCompany, session.ActiveCompanyId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        Assert.Equal(
            targetCompany.ToString(),
            jwt.Claims.Single(claim => claim.Type == "companyId").Value);
    }

    [Fact]
    public async Task SwitchCompany_CompanyNotOnMembership_Returns404()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var currentCompany = Guid.NewGuid();
        await factory.AddMembershipAsync(user.Id, currentCompany, isDefault: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateTokenFor(user, currentCompany));

        var response = await client.PostAsJsonAsync(SwitchRoute, new { companyId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
