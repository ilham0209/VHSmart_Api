using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Roles;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.Roles;

public class RoleApiTests
{
    private const string RolesRoute = "/api/admin/roles";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static HttpClient CreateAuthorizedClient(RolesApiFactory factory, bool isPlatformAdmin = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, isPlatformAdmin));
        return client;
    }

    [Fact]
    public async Task GetRoles_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(RolesRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithoutAdminUsersPermission_ReturnsForbidden()
    {
        using var factory = new RolesApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(RolesRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_AuthenticatedNonPlatformAdmin_ReturnsForbidden()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var response = await client.GetAsync(RolesRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_PlatformAdmin_ReturnsSeededRoles()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(RolesRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllRoleResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(3, grid.TotalRecords);
        Assert.Contains(grid.Data, role => role.Name == "VH Smart Admin");
        Assert.Contains(grid.Data, role => role.Name == "Auditor / Chief Auditor");
        Assert.Contains(grid.Data, role => role.Name == "Restaurant / Premise Manager");
    }

    [Fact]
    public async Task GetPermissions_PlatformAdmin_ReturnsFullMatrix()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{RolesRoute}/{AdminRoleId}/permissions");
        var matrix = await response.Content.ReadFromJsonAsync<RolePermissionsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(matrix);
        Assert.Equal(PermissionKeys.All.Count, matrix.Permissions.Count);
        Assert.All(matrix.Permissions, permission => Assert.True(permission.CanView));
    }

    [Fact]
    public async Task GetPermissions_UnknownRole_ReturnsNotFound()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{RolesRoute}/{Guid.NewGuid()}/permissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePermissions_PlatformAdmin_StoresMatrix()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var command = new UpdateRolePermissionsCommand(
            Guid.Empty,
            [new RolePermissionInput(PermissionKeys.Dashboard, true, false, false, false)]);

        var response = await client.PutAsJsonAsync($"{RolesRoute}/{AdminRoleId}/permissions", command);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var matrix = await response.Content.ReadFromJsonAsync<RolePermissionsResponse>();
        Assert.NotNull(matrix);
        var dashboard = Assert.Single(
            matrix.Permissions, permission => permission.PermissionKey == PermissionKeys.Dashboard);
        Assert.True(dashboard.CanView);
        Assert.False(matrix.Permissions.First(
            permission => permission.PermissionKey == PermissionKeys.AdminUsers).CanView);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var live = await db.RolePermissions
            .Where(row => row.RoleId == AdminRoleId)
            .ToListAsync();
        Assert.Equal(PermissionKeys.Dashboard, Assert.Single(live).PermissionKey);
    }

    [Fact]
    public async Task UpdatePermissions_UnknownPermissionKey_ReturnsBadRequest()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var command = new UpdateRolePermissionsCommand(
            Guid.Empty,
            [new RolePermissionInput("Admin.NotAScreen", true, false, false, false)]);

        var response = await client.PutAsJsonAsync($"{RolesRoute}/{AdminRoleId}/permissions", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePermissions_AuthenticatedNonPlatformAdmin_ReturnsForbidden()
    {
        using var factory = new RolesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var command = new UpdateRolePermissionsCommand(
            Guid.Empty,
            [new RolePermissionInput(PermissionKeys.Dashboard, true, false, false, false)]);

        var response = await client.PutAsJsonAsync($"{RolesRoute}/{AdminRoleId}/permissions", command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
