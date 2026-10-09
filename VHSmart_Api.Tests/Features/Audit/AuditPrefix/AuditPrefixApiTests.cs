using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class AuditPrefixApiTests
{
    private const string AuditPrefixRoute = "/api/audit/audit-prefix";

    private static readonly Guid AuditPrefixRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(AuditPrefixApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AuditPrefixRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(AuditPrefixRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(AuditPrefixRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(Guid.NewGuid(), "MRS", null);

        var response = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(AuditPrefixRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditPrefixesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsRowAndListShowsItWithBrandName()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync("NATURAL");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(brandId, "MRS", "Natural brand");

        var createResponse = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<AuditPrefixResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("MRS", created.Prefix);
        Assert.Equal(brandId, created.BrandId);

        var listResponse = await client.GetAsync(AuditPrefixRoute);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditPrefixesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("MRS", row.AuditPrefix);
        Assert.Equal("NATURAL", row.Brand);
        Assert.Equal("Natural brand", row.Description);
    }

    [Fact]
    public async Task Create_DuplicateBrand_ReturnsConflict()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(brandId, "MRS", null);
        await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        var response = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownBrand_ReturnsBadRequest()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(Guid.NewGuid(), "MRS", null);

        var response = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingPrefix_ReturnsBadRequest()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(brandId, string.Empty, null);

        var response = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_PrefixLongerThan20_ReturnsBadRequest()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPrefixCommand(brandId, new string('x', 21), null);

        var response = await client.PostAsJsonAsync(AuditPrefixRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingRow_ReturnsBrandIdForTheEditModal()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync("SERUNAI");
        var rowId = await factory.SeedPrefixAsync("SRN", brandId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{AuditPrefixRoute}/{rowId}");
        var row = await response.Content.ReadFromJsonAsync<GetAuditPrefixByIdResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal(brandId, row.BrandId);
        Assert.Equal("SERUNAI", row.Brand);
        Assert.Equal("SRN", row.AuditPrefix);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{AuditPrefixRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsTheCompanysBrandRows()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedBrandAsync("NATURAL");
        await factory.SeedBrandAsync("SERUNAI");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{AuditPrefixRoute}/options");
        var options = await response.Content.ReadFromJsonAsync<AuditPrefixOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(
            new[] { "NATURAL", "SERUNAI" },
            options.Brands.Select(row => row.Name));
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        var rowId = await factory.SeedPrefixAsync("MRS", brandId);
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditPrefixCommand(rowId, brandId, "MRS2", "Updated");

        var response = await client.PutAsJsonAsync($"{AuditPrefixRoute}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<AuditPrefixResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("MRS2", updated.Prefix);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.AuditPrefixes.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("MRS2", stored.Prefix);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var brandId = await factory.SeedBrandAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditPrefixCommand(Guid.NewGuid(), brandId, "MRS", null);

        var response = await client.PutAsJsonAsync($"{AuditPrefixRoute}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_TakenBrandSlot_ReturnsConflict()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var takenBrand = await factory.SeedBrandAsync("SERUNAI");
        await factory.SeedPrefixAsync("SRN", takenBrand);
        var rowId = await factory.SeedPrefixAsync("MRS");
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditPrefixCommand(rowId, takenBrand, "MRS", null);

        var response = await client.PutAsJsonAsync($"{AuditPrefixRoute}/{rowId}", command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedPrefixAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{AuditPrefixRoute}/{rowId}");
        var getResponse = await client.GetAsync($"{AuditPrefixRoute}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditPrefixApiFactory(AuditPrefixRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{AuditPrefixRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
