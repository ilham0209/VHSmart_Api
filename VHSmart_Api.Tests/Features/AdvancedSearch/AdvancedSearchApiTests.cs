using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VHSmart_Api.Tests.Features.AdvancedSearch;

// Routing / status-code coverage for api/advanced-search: default deny (401), the
// AdvancedSearch key (403 without it), and one 200 per endpoint with it - the search with a
// bound DateOnly filter (the panel's date range), the Excel export and the options list.
public class AdvancedSearchApiTests
{
    private static HttpClient CreateAuthorizedClient(AdvancedSearchApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Search_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AdvancedSearchApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/advanced-search/premises");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AdvancedSearchApiFactory(grantView: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/advanced-search/premises");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithPermission_ReturnsOkWithBothPremises()
    {
        using var factory = new AdvancedSearchApiFactory();
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/advanced-search/premises");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(2, body.GetProperty("totalRecords").GetInt32());
        var rows = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            ["Kedai Kopi Dummy", "Seri Rasa Factory"],
            rows.Select(row => row.GetProperty("storeName").GetString()));
    }

    [Fact]
    public async Task Search_WithQueryStringFilters_BindsTextAndDateOnlyValues()
    {
        using var factory = new AdvancedSearchApiFactory();
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var byStoreCode = await client.GetAsync(
            "/api/advanced-search/premises?StoreCode=sr-001");
        Assert.Equal(HttpStatusCode.OK, byStoreCode.StatusCode);
        var storeCodeBody = await ReadJsonAsync(byStoreCode);
        Assert.Equal(1, storeCodeBody.GetProperty("totalRecords").GetInt32());
        Assert.Equal(
            "Seri Rasa Factory",
            storeCodeBody.GetProperty("data")[0].GetProperty("storeName").GetString());

        var byOpeningDay = await client.GetAsync(
            "/api/advanced-search/premises?OpeningDateFrom=2026-06-15&OpeningDateTo=2026-06-15");
        Assert.Equal(HttpStatusCode.OK, byOpeningDay.StatusCode);
        var openingBody = await ReadJsonAsync(byOpeningDay);
        Assert.Equal(1, openingBody.GetProperty("totalRecords").GetInt32());
        Assert.Equal(
            "Seri Rasa Factory",
            openingBody.GetProperty("data")[0].GetProperty("storeName").GetString());
    }

    [Fact]
    public async Task Export_WithPermission_ReturnsXlsxBytes()
    {
        using var factory = new AdvancedSearchApiFactory();
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/advanced-search/premises/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        var fileName = response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(fileName);
        Assert.Contains("AdvancedSearch-Premise-", fileName);
    }

    [Fact]
    public async Task PremiseOptions_WithPermission_ReturnsDocumentStatusList()
    {
        using var factory = new AdvancedSearchApiFactory();
        await factory.SeedAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/advanced-search/premise-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var documentStatuses = body.GetProperty("premiseDocumentStatuses")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToList();
        Assert.Contains("COMPLETE DOCUMENTATION", documentStatuses);
        Assert.Contains("NOT COMPLETE DOCUMENTATION", documentStatuses);
        Assert.Contains("5 OF THE DOCUMENT HAS EXPIRED", documentStatuses);
        Assert.Contains(
            "ACTIVE",
            body.GetProperty("premiseStatuses").EnumerateArray().Select(value => value.GetString()));
    }
}
