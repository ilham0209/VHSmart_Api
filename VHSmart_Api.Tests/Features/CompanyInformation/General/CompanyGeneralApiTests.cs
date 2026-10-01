using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Features.CompanyInformation.General;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.CompanyInformation.General;

public class CompanyGeneralApiTests
{
    private const string Route = "/api/company-information/companies";

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        CompanyGeneralApiFactory factory,
        Guid companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(companyId));
        return client;
    }

    private static async Task<UpdateCompanyGeneralCommand> CommandForAsync(
        CompanyGeneralApiFactory factory,
        string name = "Acme Foods")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var malaysiaId = await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

        return CompanyGeneralTestData.ValidCommand(malaysiaId, name: name);
    }

    [Fact]
    public async Task GetCurrent_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CompanyGeneralApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{Route}/current");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrent_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new CompanyGeneralApiFactory(grantView: false, grantEdit: false);
        await factory.SeedDatabaseAsync();
        var companyId = await factory.SeedCompanyAsync();
        using var client = CreateAuthorizedClient(factory, companyId);

        var response = await client.GetAsync($"{Route}/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrent_WithPermission_ReturnsOwnCompany()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        var companyId = await factory.SeedCompanyAsync("Acme Foods");
        using var client = CreateAuthorizedClient(factory, companyId);

        var response = await client.GetAsync($"{Route}/current");
        var company = await response.Content.ReadFromJsonAsync<CompanyGeneralResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(company);
        Assert.Equal(companyId, company.Id);
        Assert.Equal("Acme Foods", company.Name);
        Assert.Equal("Acme Foods CB", company.CertificationBodyName);
    }

    [Fact]
    public async Task GetCurrent_UnknownCompany_ReturnsNotFound()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Somebody Else");
        using var client = CreateAuthorizedClient(factory, Guid.NewGuid());

        var response = await client.GetAsync($"{Route}/current");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsD13Lists()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory, Guid.NewGuid());

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
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new CompanyGeneralApiFactory(grantView: true, grantEdit: false);
        await factory.SeedDatabaseAsync();
        var companyId = await factory.SeedCompanyAsync();
        using var client = CreateAuthorizedClient(factory, companyId);
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/current", command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithEditPermission_StoresChanges()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        var companyId = await factory.SeedCompanyAsync("Old Name");
        using var client = CreateAuthorizedClient(factory, companyId);
        var command = await CommandForAsync(factory, name: "New Name");

        var response = await client.PutAsJsonAsync($"{Route}/current", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<CompanyGeneralResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(companyId, updated.Id);
        Assert.Equal("New Name", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.Equal("New Name", stored.Name);
    }

    [Fact]
    public async Task Update_MissingRequiredField_ReturnsBadRequest()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        var companyId = await factory.SeedCompanyAsync();
        using var client = CreateAuthorizedClient(factory, companyId);
        var command = await CommandForAsync(factory);
        command = command with { Name = string.Empty };

        var response = await client.PutAsJsonAsync($"{Route}/current", command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownCompany_ReturnsNotFound()
    {
        using var factory = new CompanyGeneralApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Somebody Else");
        using var client = CreateAuthorizedClient(factory, Guid.NewGuid());
        var command = await CommandForAsync(factory);

        var response = await client.PutAsJsonAsync($"{Route}/current", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
