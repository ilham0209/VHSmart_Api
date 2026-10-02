using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

public class CompanyProfileApiTests
{
    private const string Route = "/api/company-information/profiles";

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        CompanyProfileApiFactory factory,
        bool isPlatformAdmin = false)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", factory.CreateToken(factory.CompanyId, isPlatformAdmin));
        return client;
    }

    private static object UpdateBody(
        Guid? contactPersonStaffId = null,
        Guid? halalExecutiveStaffId = null,
        int? numberOfEmployees = 42) =>
        new
        {
            contactPersonStaffId,
            contactPersonWorkingHourFrom = "09:00:00",
            contactPersonWorkingHourTo = "17:30:00",
            halalExecutiveStaffId,
            halalExecutiveWorkingHourFrom = "08:00:00",
            halalExecutiveWorkingHourTo = "17:00:00",
            numberOfEmployees
        };

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CompanyProfileApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new CompanyProfileApiFactory(grantView: false, grantEdit: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new CompanyProfileApiFactory(grantEdit: false);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{factory.CompanyId}", UpdateBody(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_CompanyUser_ShowsOwnCompanyOnly()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        await factory.SeedCompanyAsync("Other Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetCompanyProfilesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Acme Foods", row.CompanyName);
        Assert.Equal("Acme Foods CB", row.CertificationBodyName);
    }

    [Fact]
    public async Task GetAll_PlatformAdmin_ShowsEveryCompany()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        await factory.SeedCompanyAsync("Other Foods");
        using var client = CreateAuthorizedClient(factory, isPlatformAdmin: true);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetCompanyProfilesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(2, grid.TotalRecords);
    }

    [Fact]
    public async Task Update_ValidBody_ThenProfileAndStaffOptionsShowIt()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        var staffId = await factory.SeedStaffAsync(factory.CompanyId, "Aiman Rahman");
        using var client = CreateAuthorizedClient(factory);

        var updateResponse = await client.PutAsJsonAsync(
            $"{Route}/{factory.CompanyId}",
            UpdateBody(contactPersonStaffId: staffId, numberOfEmployees: 42),
            CancellationToken.None);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CompanyProfileResponse>(Json);
        var profileResponse = await client.GetAsync($"{Route}/{factory.CompanyId}");
        var profile = await profileResponse.Content.ReadFromJsonAsync<CompanyProfileResponse>(Json);
        var optionsResponse = await client.GetAsync($"{Route}/{factory.CompanyId}/staff-options");
        var options = await optionsResponse.Content.ReadFromJsonAsync<
            IReadOnlyList<ProfileStaffOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(staffId, updated.ContactPerson?.StaffId);
        Assert.Equal(new TimeOnly(9, 0), updated.ContactPerson?.WorkingHourFrom);
        Assert.Equal(42, updated.NumberOfEmployees);

        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal("Aiman Rahman", profile.ContactPerson?.Name);
        Assert.Equal("Halal Executive", profile.ContactPerson?.Designation);
        Assert.Null(profile.HalalExecutive);

        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        Assert.NotNull(options);
        var option = Assert.Single(options);
        Assert.Equal("Aiman Rahman", option.Name);
    }

    [Fact]
    public async Task Update_UnknownStaff_ReturnsBadRequest()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{factory.CompanyId}",
            UpdateBody(contactPersonStaffId: Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_NegativeHeadcount_ReturnsBadRequest()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{factory.CompanyId}",
            UpdateBody(numberOfEmployees: -1),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_AnotherCompaniesProfile_ReturnsNotFound()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        var foreignId = await factory.SeedCompanyAsync("Foreign Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{foreignId}", UpdateBody(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownCompany_ReturnsNotFound()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AnotherCompaniesProfile_ReturnsNotFound()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        var foreignId = await factory.SeedCompanyAsync("Foreign Foods");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{foreignId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStaffOptions_AnotherCompaniesStaff_ReturnsNotFound()
    {
        using var factory = new CompanyProfileApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Acme Foods", factory.CompanyId);
        var foreignId = await factory.SeedCompanyAsync("Foreign Foods");
        await factory.SeedStaffAsync(foreignId, "Foreign Person");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{foreignId}/staff-options");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
