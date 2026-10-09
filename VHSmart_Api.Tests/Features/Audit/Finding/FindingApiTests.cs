using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class FindingApiTests
{
    private const string FindingRoute = "/api/audit/finding";

    private static readonly Guid FindingRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(FindingApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(FindingRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(FindingRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new FindingApiFactory(FindingRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(FindingRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new FindingApiFactory(FindingRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand("Finding", "F111", null, [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(FindingRoute, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(FindingRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllFindingsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsRowAndListShowsTheSelection()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync("Santan Berkualiti", "R111");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand(
            "Portion sizes are consistent with the menu descriptions.",
            "Portion sizes",
            "Long audit statement",
            [recoId]);

        var createResponse = await client.PostAsJsonAsync(FindingRoute, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<FindingResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Portion sizes", created.FindingCode);
        Assert.Equal([recoId], created.Recommendations);

        var listResponse = await client.GetAsync(FindingRoute);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllFindingsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Portion sizes", row.FindingCode);
        // The Recommendation Selection cell joins the linked names after paging.
        Assert.Equal("Santan Berkualiti", row.RecommendationSelection);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync();
        await factory.SeedFindingAsync(findingCode: "F111");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand("Another finding", "F111", null, [recoId]);

        var response = await client.PostAsJsonAsync(FindingRoute, command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRecommendations_ReturnsBadRequest()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand("Finding", "F111", null, []);

        var response = await client.PostAsJsonAsync(FindingRoute, command, Json);

        // Spec 14.4 [CONFIRMED / MANUAL]: a Finding must be linked to at least one
        // Recommendation - validation runs BEFORE the handler.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownRecommendation_ReturnsBadRequest()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand("Finding", "F111", null, [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(FindingRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateFindingCommand(string.Empty, "F111", null, [recoId]);

        var response = await client.PostAsJsonAsync(FindingRoute, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingRow_ReturnsTheDetail()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync();
        var rowId = await factory.SeedFindingAsync("Portion sizes are consistent.", "Portion sizes");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.FindingRecommendations.Add(new FindingRecommendationEntity
        {
            CompanyId = factory.CompanyId,
            FindingId = rowId,
            RecommendationId = recoId
        });
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{FindingRoute}/{rowId}");
        var row = await response.Content.ReadFromJsonAsync<GetFindingByIdResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal("Portion sizes", row.FindingCode);
        Assert.Equal([recoId], row.RecommendationIds);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{FindingRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync();
        var rowId = await factory.SeedFindingAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateFindingCommand(
            rowId, "New name", "F222", "Updated", [recoId]);

        var response = await client.PutAsJsonAsync($"{FindingRoute}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<FindingResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("New name", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.Findings.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("New name", stored.Name);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var recoId = await factory.SeedRecommendationAsync();
        using var client = CreateAuthorizedClient(factory);
        // Valid body (the validator must pass) - only the finding id is unknown.
        var command = new UpdateFindingCommand(
            Guid.NewGuid(), "Name", "F111", null, [recoId]);

        var response = await client.PutAsJsonAsync($"{FindingRoute}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedFindingAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{FindingRoute}/{rowId}");
        var getResponse = await client.GetAsync($"{FindingRoute}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_SendsInAppNotificationToTheCompanysActiveUsers()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedFindingAsync(findingCode: "Portion sizes");
        var recipientId = await factory.SeedActiveUserAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{FindingRoute}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var notification = Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(recipientId, notification.UserId);
        Assert.Equal(factory.CompanyId, notification.CompanyId);
        Assert.Equal("Finding deleted", notification.Subject);
        Assert.Contains("Portion sizes", notification.Message);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new FindingApiFactory(FindingRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{FindingRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
