using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class CompanyApiTests
{
    private const string Route = "/api/admin/companies";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        CompaniesApiFactory factory,
        bool isPlatformAdmin = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, isPlatformAdmin));
        return client;
    }

    private static async Task<CreateCompanyCommand> CommandForAsync(
        CompaniesApiFactory factory,
        string name = "Acme Foods",
        Guid? brandId = null)
    {
        var certificationBodyId =
            await factory.SeedCertificationBodyAsync($"{name} CB");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var malaysiaId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

        return new CreateCompanyCommand(
            name,
            certificationBodyId,
            "Companies Commission Of Malaysia",
            $"BRN-{Guid.NewGuid():N}",
            "Muslim Owner",
            "Jalan Perusahaan 1",
            "Jalan Perusahaan 2",
            null,
            "50000",
            "Shah Alam",
            "Gombak",
            "Selangor",
            malaysiaId,
            "0300000000",
            null,
            null,
            null,
            null,
            $"{Guid.NewGuid():N}@example.com",
            new DateTime(1990, 6, 15),
            null,
            null,
            null,
            brandId is null ? null : [brandId.Value]);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedCompanyUser_ReturnsForbidden()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsD13Lists()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<CompanyOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(
            new[] { "Companies Commission Of Malaysia" },
            options.RegistrationTypes);
        Assert.Equal(new[] { "Overseas", "Domestic" }, options.Markets);
    }

    [Fact]
    public async Task GetOptions_CompanyUser_ReturnsForbidden()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: false);

        var response = await client.GetAsync($"{Route}/options");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidCommand_ThenListAndDetailShowIt()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var brandId = await factory.SeedBrandAsync("RTW");
        var command = await CommandForAsync(factory, "Acme Foods", brandId);

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<CompanyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Acme Foods", created.Name);
        Assert.Equal(["RTW"], created.Brands.Select(brand => brand.Name));

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content
            .ReadFromJsonAsync<DataGridResponse<GetAllCompaniesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal(created.Id, row.Id);
        Assert.Equal("RTW", row.Brand);
        Assert.Equal("Acme Foods CB", row.CertificationBody);

        var detailResponse = await client.GetAsync($"{Route}/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<CompanyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("Jalan Perusahaan 1", detail.Address1);
        Assert.Equal(["RTW"], detail.Brands.Select(brand => brand.Name));
    }

    [Fact]
    public async Task Create_DuplicateBusinessRegistrationNo_ReturnsConflict()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
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
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);
        var invalid = command with { Name = string.Empty, Email = string.Empty };

        var response = await client.PostAsJsonAsync(Route, invalid, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingCompany_StoresChangesAndBrandDiff()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var kept = await factory.SeedBrandAsync("RTW");
        var dropped = await factory.SeedBrandAsync("SERUNAI");
        var companyId = await factory.SeedCompanyAsync("Old Name", brandIds: [kept, dropped]);
        var command = await CommandForAsync(factory, "New Name", kept);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{companyId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<CompanyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(["RTW"], updated.Brands.Select(brand => brand.Name));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var links = await db.CompanyBrands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();
        Assert.Single(links, link => link.BrandId == dropped && link.IsDeleted);
        Assert.Single(links, link => link.BrandId == kept && !link.IsDeleted);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new CompaniesApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
