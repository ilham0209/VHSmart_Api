using System.Net;
using System.Net.Http.Headers;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Dashboard;

// Routing / status-code coverage for api/dashboard: default deny (401), the Dashboard key
// (403 without it) and one 200 per endpoint with it.
public class DashboardApiTests
{
    private static readonly Guid DashboardRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static HttpClient CreateAuthorizedClient(DashboardApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(DashboardRoleId));
        return client;
    }

    [Fact]
    public async Task Summary_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Summary_WithPermission_ReturnsOk()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId);
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RecentRawMaterials_WithPermission_ReturnsOk()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId);
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/dashboard/recent-raw-materials");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiringTyphoid_WithPermission_ReturnsOk()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId);
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/dashboard/expiring-typhoid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RawMaterialByCountry_WithPermission_ReturnsOk()
    {
        using var factory = new DashboardApiFactory(DashboardRoleId);
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/dashboard/raw-material-by-country");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
