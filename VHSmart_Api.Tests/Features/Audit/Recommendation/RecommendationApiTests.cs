using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class RecommendationApiTests
{
    private const string RecommendationRoute = "/api/audit/recommendation";

    private static readonly Guid RecommendationRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(RecommendationApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(RecommendationRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(RecommendationRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(RecommendationRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateRecommendationCommand("Santan Berkualiti", "R111", null);

        var response = await client.PostAsJsonAsync(RecommendationRoute, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(RecommendationRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllRecommendationsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsRowAndListShowsIt()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateRecommendationCommand(
            "Santan Berkualiti", "R111", "Kelapa berkualiti");

        var createResponse = await client.PostAsJsonAsync(RecommendationRoute, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRecommendationResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Santan Berkualiti", created.Name);
        Assert.Equal("R111", created.RecommendationCode);

        var listResponse = await client.GetAsync(RecommendationRoute);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllRecommendationsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Santan Berkualiti", row.Name);
        Assert.Equal("Kelapa berkualiti", row.Description);
    }

    [Fact]
    public async Task Create_DuplicateCodeAndName_ReturnsOk_NoDuplicateRule()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateRecommendationCommand("Santan Berkualiti", "R111", null);
        await client.PostAsJsonAsync(RecommendationRoute, command, Json);

        // Spec 14.3: codes are free text; no uniqueness rule exists.
        var response = await client.PostAsJsonAsync(RecommendationRoute, command, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateRecommendationCommand(string.Empty, "R111", null);

        var response = await client.PostAsJsonAsync(RecommendationRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRecommendationCode_ReturnsBadRequest()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateRecommendationCommand("Santan Berkualiti", string.Empty, null);

        var response = await client.PostAsJsonAsync(RecommendationRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingRow_ReturnsTheDetail()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRecommendationAsync("Santan Berkualiti", "R111");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{RecommendationRoute}/{rowId}");
        var row = await response.Content.ReadFromJsonAsync<GetRecommendationByIdResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal("Santan Berkualiti", row.Name);
        Assert.Equal("R111", row.RecommendationCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{RecommendationRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRecommendationAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateRecommendationCommand(rowId, "New name", "R222", "Updated");

        var response = await client.PutAsJsonAsync($"{RecommendationRoute}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<UpdateRecommendationResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("New name", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.Recommendations.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("New name", stored.Name);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateRecommendationCommand(Guid.NewGuid(), "Name", "R111", null);

        var response = await client.PutAsJsonAsync($"{RecommendationRoute}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRecommendationAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{RecommendationRoute}/{rowId}");
        var getResponse = await client.GetAsync($"{RecommendationRoute}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new RecommendationApiFactory(RecommendationRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{RecommendationRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
