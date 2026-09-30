using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class GeneralDataApiTests
{
    private const string GeneralDataRoute = "/api/admin/general-data";

    private static readonly Guid GeneralDataRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(GeneralDataApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(GeneralDataRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(GeneralDataRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(GeneralDataRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Amanah", null);

        var response = await client.PostAsJsonAsync(GeneralDataRoute, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(GeneralDataRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllGeneralDataResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsGroupAsStringAndListShowsRow()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGeneralDataCommand(
            GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", "Own brand");

        var createResponse = await client.PostAsJsonAsync(GeneralDataRoute, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGeneralDataResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(GeneralDataGroup.COMPANY, created.Group);

        // Spec 11: the wire value is "COMPANY", the spec's uppercase value, not a number.
        var rawCreate = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"group\":\"COMPANY\"", rawCreate);

        var listResponse = await client.GetAsync(GeneralDataRoute);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllGeneralDataResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal("Sahih Mart", Assert.Single(grid.Data).Name);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Brand", "Sahih Mart", null);
        await client.PostAsJsonAsync(GeneralDataRoute, command, Json);

        var response = await client.PostAsJsonAsync(GeneralDataRoute, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_CategoryOutsideCatalogue_ReturnsBadRequest()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGeneralDataCommand(GeneralDataGroup.COMPANY, "Audit Type", "Sahih Mart", null);

        var response = await client.PostAsJsonAsync(GeneralDataRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingGroup_ReturnsBadRequest()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGeneralDataCommand(null, "Brand", "Sahih Mart", null);

        var response = await client.PostAsJsonAsync(GeneralDataRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{GeneralDataRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCatalog_Authenticated_ReturnsSevenGroupsAndTwentySevenPairs()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{GeneralDataRoute}/catalog");
        var catalog = await response.Content.ReadFromJsonAsync<List<GeneralDataCatalogResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(catalog);
        Assert.Equal(7, catalog.Count);
        Assert.Equal(27, catalog.Sum(item => item.Categories.Count));
        Assert.Contains(catalog, item => item.Group == GeneralDataGroup.AUDIT && item.Categories.Count == 7);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync(GeneralDataGroup.COMPANY, "Brand", "Old name");
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateGeneralDataCommand(
            rowId, GeneralDataGroup.COMPANY, "Ownership Type", "New name", "Updated");

        var response = await client.PutAsJsonAsync($"{GeneralDataRoute}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<UpdateGeneralDataResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("New name", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.GeneralData.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Ownership Type", stored.Category);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateGeneralDataCommand(
            Guid.NewGuid(), GeneralDataGroup.COMPANY, "Brand", "Name", null);

        var response = await client.PutAsJsonAsync($"{GeneralDataRoute}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync(GeneralDataGroup.COMPANY, "Brand", "Doomed");
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{GeneralDataRoute}/{rowId}");
        var getResponse = await client.GetAsync($"{GeneralDataRoute}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new GeneralDataApiFactory(GeneralDataRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{GeneralDataRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
