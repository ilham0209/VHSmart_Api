using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VHSmart_Api.Tests.Features.Reference;

// Routing / status-code coverage for api/reference: default deny (401), the Reference.View
// key (403 without it), and the two read endpoints with it.
public class ReferenceApiTests
{
    private static HttpClient CreateAuthorizedClient(ReferenceApiFactory factory)
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
    public async Task WebLinks_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ReferenceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reference/web-links");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WebLinks_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new ReferenceApiFactory(grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/reference/web-links");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WebLinks_WithPermission_ReturnsCardList()
    {
        using var factory = new ReferenceApiFactory();
        await factory.SeedDatabaseAsync("Verify Halal");
        await factory.SeedDatabaseAsync("VH Smart");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/reference/web-links");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var cards = body.EnumerateArray().ToList();
        Assert.Equal(2, cards.Count);
        Assert.Equal(
            ["Verify Halal", "VH Smart"],
            cards.Select(card => card.GetProperty("name").GetString()));
        Assert.Equal(
            "https://verifyhalal.example.org",
            cards[0].GetProperty("webpage").GetString());
    }

    [Fact]
    public async Task WebLinkIcon_WithPermission_ReturnsStoredBytes()
    {
        using var factory = new ReferenceApiFactory();
        var id = await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"/api/reference/web-links/{id}/icon");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((byte[])[137, 80, 78, 71], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task WebLinkIcon_UnknownId_ReturnsNotFound()
    {
        using var factory = new ReferenceApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/reference/web-links/{Guid.NewGuid()}/icon");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
