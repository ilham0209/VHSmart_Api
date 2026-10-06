using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

public class MenuApiTests
{
    private const string Route = "/api/product/manage-menus";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(MenuApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<CreateMenuCommand> CommandForAsync(MenuApiFactory factory) =>
        new(
            "Nasi Lemak",
            await factory.SeedMenuCategoryAsync(),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            "Everyday rice set",
            [await factory.SeedCompanyAsync("Sharing Partner")],
            [await factory.SeedRawMaterialAsync("Cocomilk")]);

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new MenuApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new MenuApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var partnerId = await factory.SeedCompanyAsync("Sharing Partner");
        await factory.SeedMenuAsync(
            categoryId: await factory.SeedMenuCategoryAsync(),
            accessibleCompanyId: partnerId,
            rawMaterialId: await factory.SeedRawMaterialAsync("Cocomilk"));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllMenusResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecords);

        var row = payload.Data.Single();
        Assert.Equal("Nasi Lemak", row.Name);
        Assert.Equal("Everyday rice set", row.Description);
        Assert.Equal("Permanent", row.Category);
        Assert.Equal(MenuStatus.Active, row.Status);
        Assert.Equal("Cocomilk", Assert.Single(row.Ingredients));
        Assert.Equal("Sharing Partner", Assert.Single(row.Companies));
    }

    [Fact]
    public async Task GetOptions_ReturnsTheFormDropdowns()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedMenuCategoryAsync("Seasonal");
        await factory.SeedMenuCategoryAsync("Permanent");
        await factory.SeedMenuCategoryAsync("Foreign", Guid.NewGuid());
        await factory.SeedBrandAsync();
        await factory.SeedCompanyAsync("Beta Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var payload = await ReadAsync<MenuOptionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(
            new[] { "Permanent", "Seasonal" },
            payload.Categories.Select(row => row.Name).ToArray());
        // The company picker is not tenant scoped - sharing names other companies.
        Assert.Contains(payload.Companies, row => row.Name == "Beta Foods");
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_RowOfAnotherCompany_ReturnsNotFound()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var foreignCompanyId = await factory.SeedCompanyAsync("Beta Foods");
        var id = await factory.SeedMenuAsync(companyId: foreignCompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsTheFormFieldsAndBothLists()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var partnerId = await factory.SeedCompanyAsync("Sharing Partner");
        var materialId = await factory.SeedRawMaterialAsync("Cocomilk");
        var id = await factory.SeedMenuAsync(
            categoryId: await factory.SeedMenuCategoryAsync(),
            accessibleCompanyId: partnerId,
            rawMaterialId: materialId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{id}");
        var payload = await ReadAsync<MenuResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Nasi Lemak", payload.Name);
        Assert.Equal("Permanent", payload.Category);
        Assert.Equal(MenuStatus.Active, payload.Status);
        Assert.Equal(partnerId, Assert.Single(payload.AccessibleFor).CompanyId);
        Assert.Equal("Sharing Partner", Assert.Single(payload.AccessibleFor).CompanyName);
        Assert.Equal(materialId, Assert.Single(payload.RawMaterials).Id);
        Assert.Equal("Cocomilk", Assert.Single(payload.RawMaterials).Ingredient);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsOkAndTheRowIsListed()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);
        var payload = await ReadAsync<MenuResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Nasi Lemak", payload.Name);
        Assert.Equal(MenuStatus.Active, payload.Status);
        Assert.Equal("Sharing Partner", Assert.Single(payload.AccessibleFor).CompanyName);
        Assert.Equal("Cocomilk", Assert.Single(payload.RawMaterials).Ingredient);

        var list = await ReadAsync<DataGridResponse<GetAllMenusResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalRecords);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        await client.PostAsJsonAsync(Route, command, Json);
        var second = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutListOfCompanies_ReturnsBadRequest()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(
            Route, command with { AccessibleCompanyIds = null }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutListOfRawMaterials_ReturnsBadRequest()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(
            Route, command with { RawMaterialIds = [] }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Existing_SavesTheNewName()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var id = await factory.SeedMenuAsync(
            name: "Old Name",
            categoryId: command.CategoryId!.Value);

        var response = await client.PutAsJsonAsync($"{Route}/{id}", command, Json);
        var payload = await ReadAsync<MenuResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Nasi Lemak", payload.Name);

        var stored = await ReadAsync<MenuResponse>(await client.GetAsync($"{Route}/{id}"));
        Assert.NotNull(stored);
        Assert.Equal("Nasi Lemak", stored.Name);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContentAndTheRowIsGone()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedMenuAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await ReadAsync<DataGridResponse<GetAllMenusResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(0, list.TotalRecords);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"{Route}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_SeveralRows_ReturnsNoContent()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var first = await factory.SeedMenuAsync(name: "Nasi Lemak");
        var second = await factory.SeedMenuAsync(name: "Mee Goreng");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteMenusCommand([first, second]), Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await ReadAsync<DataGridResponse<GetAllMenusResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(0, list.TotalRecords);
    }

    [Fact]
    public async Task BulkDelete_WithoutIds_ReturnsBadRequest()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteMenusCommand(null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_UnknownId_ReturnsNotFoundAndDeletesNothing()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedMenuAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/bulk-delete", new BulkDeleteMenusCommand([id, Guid.NewGuid()]), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var list = await ReadAsync<DataGridResponse<GetAllMenusResponse>>(
            await client.GetAsync(Route));
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalRecords);
    }

    [Fact]
    public async Task GetIngredientOptions_UnknownMenu_ReturnsNotFound()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/ingredients/options");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredientOptions_ExistingMenu_OffersOnlyWhatIsNotAttachedYet()
    {
        using var factory = new MenuApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var attachedId = await factory.SeedRawMaterialAsync("Cocomilk");
        var freeId = await factory.SeedRawMaterialAsync("Cane Sugar");
        var id = await factory.SeedMenuAsync(rawMaterialId: attachedId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{id}/ingredients/options");
        var payload = await ReadAsync<DataGridResponse<MenuIngredientResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(freeId, Assert.Single(payload.Data).Id);
    }
}
