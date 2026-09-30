using System.Net;
using System.Net.Http.Headers;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class PermissionTests
{
    private const string PermissionRoute = "/api/test/permissions";
    private const string OpenRoute = "/api/test/open";

    private static readonly Guid ViewerRoleId = Guid.NewGuid();
    private static readonly Guid StrangerRoleId = Guid.NewGuid();

    [Fact]
    public async Task Get_ViewWithoutToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{PermissionRoute}/view");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ViewWithoutPermission_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, StrangerRoleId);

        var response = await client.GetAsync($"{PermissionRoute}/view");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ViewWithPermission_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, ViewerRoleId);

        var response = await client.GetAsync($"{PermissionRoute}/view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_DeleteWithOnlyViewPermission_ReturnsForbidden()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, ViewerRoleId);

        var response = await client.GetAsync($"{PermissionRoute}/delete");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutHasPermission_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, StrangerRoleId);

        var response = await client.GetAsync($"{PermissionRoute}/authenticated-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_EndpointWithoutAuthorizationMetadata_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(OpenRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_EndpointWithoutAuthorizationMetadata_WithToken_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, StrangerRoleId);

        var response = await client.GetAsync(OpenRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static PermissionApiFactory CreateFactory() =>
        new((ViewerRoleId, PermissionKeys.AuditRecommendation, PermissionAction.View));

    private static void Authorize(HttpClient client, PermissionApiFactory factory, Guid roleId) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(roleId));
}
