using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.CertificationBodies;

public class CertificationBodyApiTests
{
    private const string Route = "/api/admin/certification-bodies";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        CertificationBodiesApiFactory factory,
        bool isPlatformAdmin = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, isPlatformAdmin));
        return client;
    }

    private static CreateCertificationBodyCommand NewCommand(string name = "JAKIM")
    {
        // CountryId comes from the seeded Malaysia row; tests replace it with a fresh factory's
        // country after seeding the database.
        return new CreateCertificationBodyCommand(
            name, null, null, null,
            "Jalan Gombak", null, "Kuala Lumpur", "50450",
            Guid.Empty, "Wilayah Persekutuan", "0380000000", null, null,
            "info@jakim.example", "Director", null, null);
    }

    private static async Task<CreateCertificationBodyCommand> CommandForAsync(
        CertificationBodiesApiFactory factory,
        string name = "JAKIM")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var malaysiaId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var template = NewCommand(name);
        return template with { CountryId = malaysiaId };
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedCompanyUser_ReturnsForbidden()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidCommand_ThenListAndDetailShowIt()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<CertificationBodyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("JAKIM", created.Name);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllCertificationBodiesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Malaysia", row.CountryName);

        var detailResponse = await client.GetAsync($"{Route}/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<CertificationBodyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("info@jakim.example", detail.Email);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        await client.PostAsJsonAsync(Route, command, Json);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRequiredField_ReturnsBadRequest()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var invalid = command with { Name = string.Empty, Email = string.Empty };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCountry_ReturnsBadRequest()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var invalid = command with { CountryId = Guid.NewGuid() };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("JAKIM");
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory, "JAKIM Malaysia");

        var response = await client.PutAsJsonAsync($"{Route}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<CertificationBodyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("JAKIM Malaysia", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.CertificationBodies.AsNoTracking().SingleAsync();
        Assert.Equal("Kuala Lumpur", stored.City);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("JAKIM");
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var detailResponse = await client.GetAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Logo_UploadThenGet_ReturnsBytes()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("JAKIM");
        using var client = CreateAuthorizedClient(factory);

        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([137, 80, 78, 71]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "File", "logo.png");

        var uploadResponse = await client.PostAsync($"{Route}/{rowId}/logo", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadCertificationBodyLogoResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal("logo.png", uploaded.FileName);

        var getResponse = await client.GetAsync($"{Route}/{rowId}/logo");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([137, 80, 78, 71], await getResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Logo_GetWithoutLogo_ReturnsNotFound()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("JAKIM");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{rowId}/logo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLogo_CompanyUser_ReturnsForbidden()
    {
        using var factory = new CertificationBodiesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync("JAKIM");
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var response = await client.GetAsync($"{Route}/{rowId}/logo");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
