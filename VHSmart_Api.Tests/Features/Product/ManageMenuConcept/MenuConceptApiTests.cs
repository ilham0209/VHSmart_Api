using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Product.ManageMenuConcept;

public class MenuConceptApiTests
{
    private const string Route = "/api/product/manage-menu-concepts";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(MenuConceptApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, new CreateMenuConceptCommand("Breakfast Menu", "Rotating morning set"), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Alpha Foods", id: factory.CompanyId);
        var conceptId = await factory.SeedMenuConceptAsync();
        var menuId = await factory.SeedMenuAsync(name: "Nasi Lemak");
        await factory.SeedMenuConceptLinkAsync(conceptId, menuId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllMenuConceptsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecords);

        var row = payload.Data.Single();
        Assert.Equal("Breakfast Menu", row.Name);
        Assert.Equal("Rotating morning set", row.Description);
        Assert.Equal("Alpha Foods", row.CompanyName);
        Assert.Equal("Nasi Lemak", Assert.Single(row.Menus));
        Assert.Null(row.ModifiedDate);
    }

    [Fact]
    public async Task GetAll_RowOfAnotherCompany_IsNotListed()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var foreignCompanyId = await factory.SeedCompanyAsync("Beta Foods");
        await factory.SeedMenuConceptAsync(companyId: foreignCompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllMenuConceptsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(0, payload.TotalRecords);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsTheFormFieldsAndItsMenuList()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Alpha Foods", id: factory.CompanyId);
        var conceptId = await factory.SeedMenuConceptAsync();
        var menuId = await factory.SeedMenuAsync(name: "Nasi Lemak");
        await factory.SeedMenuConceptLinkAsync(conceptId, menuId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{conceptId}");
        var payload = await ReadAsync<MenuConceptResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(conceptId, payload.Id);
        Assert.Equal("Breakfast Menu", payload.Name);
        Assert.Equal("Rotating morning set", payload.Description);
        Assert.Equal(factory.CompanyId, payload.CompanyId);
        Assert.Equal("Alpha Foods", payload.CompanyName);

        var menu = Assert.Single(payload.Menus);
        Assert.Equal(menuId, menu.MenuId);
        Assert.Equal("Nasi Lemak", menu.MenuName);
        Assert.Equal("Permanent", menu.MenuCategory);
        Assert.Equal(MenuConceptMenuMappingStatus.Active, menu.MappingStatus);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsOkOwnedByTheJwtCompany()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Alpha Foods", id: factory.CompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, new CreateMenuConceptCommand("Breakfast Menu", "Rotating morning set"), Json);
        var payload = await ReadAsync<MenuConceptResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Breakfast Menu", payload.Name);
        Assert.Equal("Rotating morning set", payload.Description);
        // "For Company*" is the caller's company taken from the JWT (CodingRules 8.1).
        Assert.Equal(factory.CompanyId, payload.CompanyId);
        Assert.Equal("Alpha Foods", payload.CompanyName);
        Assert.Empty(payload.Menus);

        var list = await ReadAsync<DataGridResponse<GetAllMenuConceptsResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalRecords);
    }

    [Fact]
    public async Task Create_WithoutName_ReturnsBadRequest()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, new CreateMenuConceptCommand(null, "Rotating morning set"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NameOver200Characters_ReturnsBadRequest()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, new CreateMenuConceptCommand(new string('a', 201), null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameTwice_ReturnsOkForBoth()
    {
        // Flagged for the owner: Database.md 9 defines no unique index for PrdMenuConcepts
        // and the legacy "concept-name check" states no scope, so no rule was invented here.
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateMenuConceptCommand("Breakfast Menu", "Rotating morning set");

        var first = await client.PostAsJsonAsync(Route, command, Json);
        var second = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Update_Existing_SavesTheFieldsAndReplacesItsMenuList()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var conceptId = await factory.SeedMenuConceptAsync(name: "Old Name");
        var linkedMenuId = await factory.SeedMenuAsync(name: "Nasi Lemak");
        var freeMenuId = await factory.SeedMenuAsync(name: "Ayam Penyet");
        await factory.SeedMenuConceptLinkAsync(conceptId, linkedMenuId);
        using var client = CreateAuthorizedClient(factory);

        var command = new UpdateMenuConceptCommand(
            conceptId, "Breakfast Menu", "Rotating morning set", [freeMenuId]);
        var response = await client.PutAsJsonAsync($"{Route}/{conceptId}", command, Json);
        var payload = await ReadAsync<MenuConceptResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Breakfast Menu", payload.Name);
        // The detail table keeps every pair with its own status (spec 9.3 has a Mapping Status
        // column): the new pair is ACTIVE, the dropped one stays as INACTIVE (PD-02).
        Assert.Equal(2, payload.Menus.Count);
        Assert.Equal(
            "Ayam Penyet",
            payload.Menus.Single(row =>
                row.MappingStatus == MenuConceptMenuMappingStatus.Active).MenuName);
        Assert.Equal(
            "Nasi Lemak",
            payload.Menus.Single(row =>
                row.MappingStatus == MenuConceptMenuMappingStatus.Inactive).MenuName);

        var stored = await ReadAsync<MenuConceptResponse>(
            await client.GetAsync($"{Route}/{conceptId}"));
        Assert.NotNull(stored);
        Assert.Equal("Breakfast Menu", stored.Name);
        Assert.Contains(stored.Menus, row =>
            row.MenuName == "Ayam Penyet"
            && row.MappingStatus == MenuConceptMenuMappingStatus.Active);
        Assert.Contains(stored.Menus, row =>
            row.MenuName == "Nasi Lemak"
            && row.MappingStatus == MenuConceptMenuMappingStatus.Inactive);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}",
            new UpdateMenuConceptCommand(Guid.NewGuid(), "Breakfast Menu", null, []),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutListOfMenu_ReturnsBadRequest()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var conceptId = await factory.SeedMenuConceptAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{conceptId}",
            new UpdateMenuConceptCommand(conceptId, "Breakfast Menu", null, null),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAMenuTheCallerCannotSee_ReturnsBadRequest()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var conceptId = await factory.SeedMenuConceptAsync();
        var foreignCompanyId = await factory.SeedCompanyAsync("Beta Foods");
        var foreignMenuId = await factory.SeedMenuAsync(
            companyId: foreignCompanyId, name: "Private Menu");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{conceptId}",
            new UpdateMenuConceptCommand(conceptId, "Breakfast Menu", null, [foreignMenuId]),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContentAndTheRowIsGone()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var conceptId = await factory.SeedMenuConceptAsync();
        var menuId = await factory.SeedMenuAsync(name: "Nasi Lemak");
        await factory.SeedMenuConceptLinkAsync(conceptId, menuId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{conceptId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await ReadAsync<DataGridResponse<GetAllMenuConceptsResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(0, list.TotalRecords);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"{Route}/{conceptId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMenuOptions_ExistingConcept_OffersOnlyWhatIsNotLinkedYet()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var conceptId = await factory.SeedMenuConceptAsync();
        var linkedMenuId = await factory.SeedMenuAsync(name: "Nasi Lemak");
        var freeMenuId = await factory.SeedMenuAsync(name: "Ayam Penyet");
        await factory.SeedMenuConceptLinkAsync(conceptId, linkedMenuId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{conceptId}/menus/options");
        var payload = await ReadAsync<DataGridResponse<MenuConceptMenuOptionResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(freeMenuId, Assert.Single(payload.Data).Id);
    }

    [Fact]
    public async Task GetMenuOptions_UnknownConcept_ReturnsNotFound()
    {
        using var factory = new MenuConceptApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/menus/options");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
