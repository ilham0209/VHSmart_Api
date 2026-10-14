using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VHSmart_Api.Tests.Features.Support;

// Routing / status-code coverage for POST api/support/feedback: default deny (401), the
// Support.Submit key (403 without it), and the stored row behind a valid form (200).
public class SupportApiTests
{
    private static HttpClient CreateAuthorizedClient(SupportApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    private static MultipartFormDataContent BuildForm(string category, string details)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(category), "Category");
        content.Add(new StringContent(details), "Details");
        return content;
    }

    [Fact]
    public async Task SubmitFeedback_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new SupportApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/support/feedback",
            BuildForm("Enquiry", "A question."));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new SupportApiFactory(grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            "/api/support/feedback",
            BuildForm("Enquiry", "A question."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_WithPermission_ReturnsTheStoredId()
    {
        using var factory = new SupportApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            "/api/support/feedback",
            BuildForm("Complaint", "The portal was slow."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task SubmitFeedback_EmptyDetails_ReturnsBadRequest()
    {
        using var factory = new SupportApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            "/api/support/feedback",
            BuildForm("Suggestion", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_CategoryOutsideTheFour_ReturnsBadRequest()
    {
        using var factory = new SupportApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            "/api/support/feedback",
            BuildForm("NotACategory", "Some details."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
