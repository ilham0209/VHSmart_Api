using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class RawMaterialApiTests
{
    private const string Route = "/api/raw-material/master-list";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(RawMaterialApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<CreateRawMaterialCommand> CommandForAsync(RawMaterialApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        return new CreateRawMaterialCommand(
            RawMaterialCategory.Core,
            await factory.SeedGeneralDataAsync("Ingredient Status", "Active"),
            "Rice Flour",
            "RM-001",
            "Rice Flour 1kg",
            "Oryza sativa",
            await factory.SeedGeneralDataAsync("Ingredient Source", "Plant Based"),
            await factory.SeedManufacturerAsync("Santan Foods"),
            false,
            [await factory.SeedCompanyAsync("Sharing Partner")]);
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsTheSeededRow()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedRowAsync(
            "Rice Flour", "RM-001", accessibleCompanyIds: [await factory.SeedCompanyAsync("Sharing Partner")]);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllRawMaterialsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecords);
        Assert.Equal("Rice Flour", payload.Data.Single().Ingredient);
        Assert.Equal("Sharing Partner", payload.Data.Single().AccessibleFor.Single());
    }

    [Fact]
    public async Task GetById_ReturnsEveryFormField()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{id}");
        var payload = await ReadAsync<RawMaterialResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(id, payload.Id);
        Assert.Equal("RM-001", payload.IngredientCode);
        Assert.Equal("Santan Foods", payload.ManufacturerName);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsTheFormDropdowns()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedGeneralDataAsync("Ingredient Status", "Active");
        await factory.SeedGeneralDataAsync("Ingredient Source", "Plant Based");
        await factory.SeedManufacturerAsync("Santan Foods");
        await factory.SeedCompanyAsync("Sharing Partner");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var payload = await ReadAsync<RawMaterialOptionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Active", Assert.Single(payload.IngredientStatuses).Name);
        Assert.Equal("Plant Based", Assert.Single(payload.IngredientSources).Name);
        Assert.Equal("Santan Foods", Assert.Single(payload.Manufacturers).Name);
        Assert.Contains(payload.Companies, company => company.Name == "Sharing Partner");
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsTheRow()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);
        var payload = await ReadAsync<RawMaterialResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("RM-001", payload.IngredientCode);
        Assert.Equal(RawMaterialCategory.Core, payload.Category);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the visibility filter compares against Guid.Empty,
        // so read the rows back unfiltered.
        Assert.Equal(1, await db.RawMaterials.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.RawMaterialAccessibleCompanies.CountAsync());
    }

    [Fact]
    public async Task Create_MissingRequiredFields_ReturnsBadRequest()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = (await CommandForAsync(factory)) with
        {
            Category = null,
            IngredientStatusId = null,
            Ingredient = null,
            IngredientSourceId = null,
            ManufacturerSupplierId = null,
            AccessibleCompanyIds = null
        };

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateIngredientCode_ReturnsConflict()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedRowAsync(ingredientCode: "RM-001");
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_ReturnsTheChangedRow()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{id}", command with { Ingredient = "Corn Flour" }, Json);
        var payload = await ReadAsync<RawMaterialResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Corn Flour", payload.Ingredient);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.RawMaterials.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Corn Flour", stored.Ingredient);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnsharedRowOfAnotherCompany_ReturnsNotFound()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var foreignId = await factory.SeedRowAsync(companyId: Guid.NewGuid());
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{foreignId}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SharedRow_IsReadableByTheSharingCompanyButNotEditable()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var sharingCompanyId = await factory.SeedCompanyAsync("Sharing Partner");
        var id = await factory.SeedRowAsync(accessibleCompanyIds: [sharingCompanyId]);

        using var ownerClient = CreateAuthorizedClient(factory);
        using var sharingClient = CreateAuthorizedClient(factory, sharingCompanyId);

        // Shared rows reach the list and the detail of the other company ...
        var list = await sharingClient.GetAsync(Route);
        var detail = await sharingClient.GetAsync($"{Route}/{id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        // ... but sharing is read-only: the edit and the delete answer 404. The payload carries
        // every tenant-scoped dropdown row (status, source, manufacturer) of the caller's OWN
        // company, so validation passes and the 404 comes from the ownership rule (validation
        // runs before every handler).
        var sharingCommand = (await CommandForAsync(factory)) with
        {
            IngredientStatusId = await factory.SeedGeneralDataAsync(
                "Ingredient Status", "Active", sharingCompanyId),
            IngredientSourceId = await factory.SeedGeneralDataAsync(
                "Ingredient Source", "Plant Based", sharingCompanyId),
            ManufacturerSupplierId = await factory.SeedManufacturerAsync(
                "Sharing Foods", sharingCompanyId)
        };
        var update = await sharingClient.PutAsJsonAsync($"{Route}/{id}", sharingCommand, Json);
        var delete = await sharingClient.DeleteAsync($"{Route}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // The owner keeps the edit, with the owner's own dropdown rows.
        var ownerUpdate = await ownerClient.PutAsJsonAsync(
            $"{Route}/{id}",
            (await CommandForAsync(factory)) with { Ingredient = "Owner Change" },
            Json);
        Assert.Equal(HttpStatusCode.OK, ownerUpdate.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var delete = await client.DeleteAsync($"{Route}/{id}");
        var list = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var payload = await ReadAsync<DataGridResponse<GetAllRawMaterialsResponse>>(list);
        Assert.NotNull(payload);
        Assert.Equal(0, payload.TotalRecords);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_SelectedRows_ReturnsNoContent()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var first = await factory.SeedRowAsync();
        var second = await factory.SeedRowAsync("Corn Flour", "RM-002");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteRawMaterialsCommand([first, second]), Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.Equal(2, await db.RawMaterials.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task BulkDelete_UnknownId_ReturnsNotFoundAndDeletesNothing()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteRawMaterialsCommand([id, Guid.NewGuid()]), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.Equal(0, await db.RawMaterials.IgnoreQueryFilters().CountAsync(row => row.IsDeleted));
    }

    [Fact]
    public async Task BulkDelete_NoRowsSelected_ReturnsBadRequest()
    {
        using var factory = new RawMaterialApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteRawMaterialsCommand([]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
