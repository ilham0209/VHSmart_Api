using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Product.VerifyHalalProductUpdate;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

public class VerifyHalalApiTests
{
    private const string Route = "/api/product/verify-halal-products";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(VerifyHalalApiFactory factory, Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedRowAsync(publishStatus: VerifyHalalPublishStatus.Published);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetVerifyHalalProductsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecords);
        var row = payload.Data.Single();
        Assert.Equal("Santan Kicap", row.ProductName);
        Assert.Equal("Sereni", row.Brand);
        Assert.Equal(VerifyHalalPublishStatus.Published, row.PublishStatus);
        Assert.Null(row.HalalApplicationNo);
    }

    [Fact]
    public async Task GetAll_ForeignCompanyRow_IsInvisible()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedRowAsync(companyId: Guid.NewGuid());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetVerifyHalalProductsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload.Data);
    }

    [Fact]
    public async Task CategoryOptions_WithPermission_ReturnsOwnCategories()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedProductCategoryAsync("Zebra Sauces");
        await factory.SeedProductCategoryAsync("Alpha Sauces");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/category-options");
        var payload = await ReadAsync<IReadOnlyList<VerifyHalalCategoryOption>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(
            new[] { "Alpha Sauces", "Zebra Sauces" },
            payload.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task CategoryOptions_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/category-options");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishStatus_WithEditPermission_ReturnsNoContent()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{id}/publish-status",
            new { publishStatus = VerifyHalalPublishStatus.Published },
            Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetVerifyHalalProductsResponse>>(fetched);
        Assert.NotNull(payload);
        Assert.Equal(VerifyHalalPublishStatus.Published, payload.Data.Single().PublishStatus);
    }

    [Fact]
    public async Task UpdatePublishStatus_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{id}/publish-status",
            new { publishStatus = VerifyHalalPublishStatus.Published },
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishStatus_UnknownProduct_ReturnsNotFound()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}/publish-status",
            new { publishStatus = VerifyHalalPublishStatus.Published },
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishStatus_InvalidValue_ReturnsBadRequest()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{id}/publish-status",
            new { publishStatus = "Draft" },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishStatus_PublishedWithoutImage_StoresTheValue()
    {
        using var factory = new VerifyHalalApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var id = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{id}/publish-status",
            new { publishStatus = VerifyHalalPublishStatus.PublishedWithoutImage },
            Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetVerifyHalalProductsResponse>>(fetched);
        Assert.NotNull(payload);
        Assert.Equal(
            VerifyHalalPublishStatus.PublishedWithoutImage,
            payload.Data.Single().PublishStatus);
    }
}
