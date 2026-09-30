using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Lookups;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Admin.Lookups;

public class LookupApiTests
{
    private const string CountriesRoute = "/api/admin/countries";

    private const string StatesRoute = "/api/admin/states";

    private const string SchemesRoute = "/api/admin/schemes";

    // Any stable role id works: the stub permission service is keyed on the token's role claim.
    private static readonly Guid LookupRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static HttpClient CreateAuthorizedClient(LookupsApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(LookupRoleId));
        return client;
    }

    [Fact]
    public async Task GetCountries_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new LookupsApiFactory(LookupRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(CountriesRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCountries_WithoutDashboardPermission_ReturnsForbidden()
    {
        using var factory = new LookupsApiFactory(LookupRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(CountriesRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStates_WithoutDashboardPermission_ReturnsForbidden()
    {
        using var factory = new LookupsApiFactory(LookupRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(StatesRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSchemes_WithoutDashboardPermission_ReturnsForbidden()
    {
        using var factory = new LookupsApiFactory(LookupRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(SchemesRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCountries_Authenticated_ReturnsSeededCountries()
    {
        using var factory = new LookupsApiFactory(LookupRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(CountriesRoute);
        var countries = await response.Content.ReadFromJsonAsync<List<GetCountriesResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(countries);
        Assert.Equal(249, countries.Count);
        Assert.Contains(countries, country => country.IsoCode == "MYS" && country.Name == "Malaysia");
    }

    [Fact]
    public async Task GetStates_AuthenticatedWithMalaysiaFilter_ReturnsSixteenStates()
    {
        using var factory = new LookupsApiFactory(LookupRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        Guid malaysiaId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
            malaysiaId = await db.Countries
                .AsNoTracking()
                .Where(country => country.IsoCode == "MYS")
                .Select(country => country.Id)
                .SingleAsync();
        }

        var response = await client.GetAsync($"{StatesRoute}?countryId={malaysiaId}");
        var states = await response.Content.ReadFromJsonAsync<List<GetStatesResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(states);
        Assert.Equal(16, states.Count);
        Assert.All(states, state => Assert.Equal(malaysiaId, state.CountryId));
    }

    [Fact]
    public async Task GetSchemes_Authenticated_ReturnsNineSchemesInOrder()
    {
        using var factory = new LookupsApiFactory(LookupRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(SchemesRoute);
        var schemes = await response.Content.ReadFromJsonAsync<List<GetSchemesResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(schemes);
        Assert.Equal(9, schemes.Count);
        Assert.Equal(
            new[] { "PR", "PM", null, "BG", "FM", "PL", "KO", "MD", "OEM" },
            schemes.Select(scheme => scheme.Code).ToArray());
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            schemes.Select(scheme => scheme.SortOrder).ToArray());
    }
}
