using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.HalalPolicy;

public class HalalPolicyApiTests
{
    private const string Route = "/api/company-information/halal-policies";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(HalalPolicyApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId));
        return client;
    }

    // The add form is multipart (spec 7.2 modal "Manage Halal Policy"): Scheme*, Upload Company
    // Halal Policy*, Halal Policy Date*. Nulls leave the field out.
    private static MultipartFormDataContent Form(
        Guid? schemeId = null,
        string? date = "2026-01-15",
        string? fileName = "policy.pdf")
    {
        var content = new MultipartFormDataContent();
        if (schemeId is not null)
            content.Add(new StringContent(schemeId.Value.ToString()), "SchemeId");
        if (date is not null)
            content.Add(new StringContent(date), "PolicyDate");
        if (fileName is not null)
        {
            var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "File", fileName);
        }

        return content;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(Route, Form(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedEmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllHalalPoliciesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidForm_ThenListAndDocumentShowIt()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var schemeId = await factory.FirstSchemeIdAsync();

        var createResponse = await client.PostAsync(Route, Form(schemeId), CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<HalalPolicyResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(schemeId, created.SchemeId);
        Assert.Equal("policy.pdf", created.FileName);
        Assert.Equal(new DateTime(2026, 1, 15), created.PolicyDate);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllHalalPoliciesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal("policy.pdf", row.FileName);
        Assert.Equal(1, row.No);
        Assert.False(string.IsNullOrWhiteSpace(row.Scheme));

        var documentResponse = await client.GetAsync($"{Route}/{created.Id}/document");

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/pdf", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([37, 80, 68, 70], await documentResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Create_DuplicateScheme_ReturnsConflict()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var schemeId = await factory.FirstSchemeIdAsync();
        await client.PostAsync(Route, Form(schemeId), CancellationToken.None);

        var response = await client.PostAsync(Route, Form(schemeId), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_ReturnsBadRequest()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            Route,
            Form(schemeId: null, date: null, fileName: null),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var schemeId = await factory.FirstSchemeIdAsync();

        var response = await client.PostAsync(
            Route,
            Form(schemeId, fileName: "installer.exe"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetDocument_UnknownId_ReturnsNotFound()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/document");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllHalalPoliciesResponse>>(Json);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new HalalPolicyApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
