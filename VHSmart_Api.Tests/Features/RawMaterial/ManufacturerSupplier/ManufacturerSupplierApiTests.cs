using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

public class ManufacturerSupplierApiTests
{
    private const string Route = "/api/raw-material/manufacturer-suppliers";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(ManufacturerSupplierApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId));
        return client;
    }

    private static CreateManufacturerSupplierCommand Command(
        Guid countryId,
        ManufacturerSupplierType? type = ManufacturerSupplierType.Both,
        string? manufacturerEmail = "manufacturer@example.com",
        string? supplierEmail = "supplier@example.com",
        string manufacturerName = "Santan Foods Sdn Bhd") =>
        new(
            type,
            manufacturerName,
            "202301001234",
            null,
            "Jalan Gombak 1",
            countryId,
            "Aminah",
            "0380000000",
            manufacturerEmail,
            "https://santan.example.com",
            "Santan Supplies",
            "Jalan Gombak 2",
            countryId,
            "Fatimah",
            "0380001111",
            supplierEmail);

    private static async Task<CreateManufacturerSupplierCommand> CommandForAsync(
        ManufacturerSupplierApiFactory factory,
        ManufacturerSupplierType? type = ManufacturerSupplierType.Both,
        string? manufacturerEmail = "manufacturer@example.com",
        string? supplierEmail = "supplier@example.com")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var malaysiaId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        return Command(malaysiaId, type, manufacturerEmail, supplierEmail);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedEmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllManufacturerSuppliersResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ThenListAndDetailShowIt()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<ManufacturerSupplierResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(ManufacturerSupplierType.Both, created.Type);
        Assert.Equal("Santan Foods Sdn Bhd", created.ManufacturerName);
        Assert.Equal("Santan Supplies", created.SupplierName);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllManufacturerSuppliersResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal("manufacturer@example.com", row.ManufacturerEmail);
        Assert.Equal("supplier@example.com", row.SupplierEmail);

        var detailResponse = await client.GetAsync($"{Route}/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ManufacturerSupplierResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("202301001234", detail.ManufacturerBusinessRegNo);
        Assert.Equal("https://santan.example.com", detail.ManufacturerWebpage);
    }

    [Fact]
    public async Task Create_DuplicateManufacturerEmail_ReturnsConflict()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        await client.PostAsJsonAsync(Route, command, Json);

        var response = await client.PostAsJsonAsync(
            Route,
            command with { ManufacturerName = "Second Manufacturer" },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateSupplierEmail_ReturnsConflict()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        await client.PostAsJsonAsync(Route, command, Json);

        var response = await client.PostAsJsonAsync(
            Route,
            command with { ManufacturerEmail = "other@example.com" },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingManufacturerFields_ReturnsBadRequest()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var invalid = command with { ManufacturerName = null, ManufacturerAddress = null, ManufacturerEmail = null };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_SupplierOnlyWithoutSupplierFields_ReturnsBadRequest()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory, ManufacturerSupplierType.SupplierOnly);
        var invalid = command with { SupplierName = null, SupplierAddress = null };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCountry_ReturnsBadRequest()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var invalid = command with { ManufacturerCountryId = Guid.NewGuid() };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChangesAndClearsTheUnusedHalf()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory, ManufacturerSupplierType.ManufacturerOnly);

        var response = await client.PutAsJsonAsync($"{Route}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<ManufacturerSupplierResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(ManufacturerSupplierType.ManufacturerOnly, updated.Type);
        Assert.Null(updated.SupplierName);
        Assert.Null(updated.SupplierEmail);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.ManufacturerSuppliers.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Santan Foods Sdn Bhd", stored.ManufacturerName);
        Assert.Equal(ManufacturerSupplierType.ManufacturerOnly, stored.Type);
        Assert.Null(stored.SupplierAddress);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var detailResponse = await client.GetAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadLogo_ThenGetLogo_ReturnsStoredBytes()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "logo.png");

        var uploadResponse = await client.PostAsync($"{Route}/{rowId}/logo", form);
        var logoResponse = await client.GetAsync($"{Route}/{rowId}/logo");

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
        Assert.Equal("image/png", logoResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await logoResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetLogo_WithoutLogo_ReturnsNotFound()
    {
        using var factory = new ManufacturerSupplierApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("Santan Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{rowId}/logo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
