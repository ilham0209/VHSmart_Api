using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.AuditCriteria;

public class AuditCriteriaApiTests
{
    private const string Route = "/api/audit/audit-criteria";

    private static readonly Guid AuditCriteriaRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(AuditCriteriaApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AuditCriteriaRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaCommand(
            Guid.NewGuid(), 0, Guid.NewGuid(), 0, null, null, null, null, null, null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Masters_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaMasterCommand(
            AuditCriteriaMasterKind.Criteria, "Cleanliness");

        var response = await client.PostAsJsonAsync($"{Route}/masters", command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditCriteriaResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsRowAndListShowsTheJoinedTexts()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync("Storage");
        var criteria = await factory.SeedMasterAsync(text: "Temperature control");
        var finding = await factory.SeedFindingAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaCommand(
            category, 2, criteria, 1, null,
            "ISO 22000", "Clause 8", 5m, "Daily logs kept", [finding]);

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<AuditCriteriaResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(5m, created.PotentialPoint);
        Assert.Equal([finding], created.Findings);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditCriteriaResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Storage", row.Category);
        Assert.Equal("Temperature control", row.Criteria);
        Assert.Equal("Daily logs kept", row.Description);
    }

    [Fact]
    public async Task Create_MissingCategory_ReturnsBadRequest()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var criteria = await factory.SeedMasterAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaCommand(
            Guid.Empty, 0, criteria, 0, null, null, null, null, null, null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsBadRequest()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var criteria = await factory.SeedMasterAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaCommand(
            Guid.NewGuid(), 0, criteria, 0, null, null, null, null, null, null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        // Validation runs BEFORE the handler (the house pipeline), so an unknown category
        // is a 400 here and only a valid-but-foreign/unknown row in the handler answers 404.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownFinding_ReturnsBadRequest()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync();
        var criteria = await factory.SeedMasterAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaCommand(
            category, 0, criteria, 0, null, null, null, null, null, [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Masters_DuplicateText_ReturnsConflict()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedMasterAsync(AuditCriteriaMasterKind.Criteria, "Cleanliness");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaMasterCommand(
            AuditCriteriaMasterKind.Criteria, "Cleanliness");

        var response = await client.PostAsJsonAsync($"{Route}/masters", command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Masters_ValidCommand_ReturnsTheNewMaster()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditCriteriaMasterCommand(
            AuditCriteriaMasterKind.SubCriteria, "Chiller logs");

        var response = await client.PostAsJsonAsync($"{Route}/masters", command, Json);
        var created = await response.Content.ReadFromJsonAsync<AuditCriteriaMasterResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(AuditCriteriaMasterKind.SubCriteria, created.Kind);
        Assert.Equal("Chiller logs", created.Text);
    }

    [Fact]
    public async Task Options_ReturnsTheModalSources()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync("Storage");
        await factory.SeedMasterAsync(text: "Temperature control");
        await factory.SeedMasterAsync(AuditCriteriaMasterKind.SubCriteria, "Chiller logs");
        await factory.SeedFindingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<AuditCriteriaOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(category, Assert.Single(options.Categories).Id);
        Assert.Equal("Temperature control", Assert.Single(options.Criteria).Text);
        Assert.Equal("Chiller logs", Assert.Single(options.SubCriteria).Text);
        Assert.Single(options.Findings);
    }

    [Fact]
    public async Task GetById_ExistingRow_ReturnsTheDetail()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync("Storage");
        var criteria = await factory.SeedMasterAsync(text: "Temperature control");
        var finding = await factory.SeedFindingAsync();
        var rowId = await factory.SeedCriteriaAsync(category, criteria, reference: "Clause 8");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.AuditCriteriaFindings.Add(new AuditCriteriaFindingEntity
        {
            CompanyId = factory.CompanyId,
            AuditCriteriaId = rowId,
            FindingId = finding
        });
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{rowId}");
        var row = await response.Content.ReadFromJsonAsync<AuditCriteriaResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal("Clause 8", row.Reference);
        Assert.Equal([finding], row.Findings);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_StoresChanges()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync("Storage");
        var criteria = await factory.SeedMasterAsync(text: "Temperature control");
        var rowId = await factory.SeedCriteriaAsync(category, criteria);
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditCriteriaCommand(
            rowId, category, 3, criteria, 4, null,
            "ISO 22000", "Clause 9", 7m, "Updated description", null);

        var response = await client.PutAsJsonAsync($"{Route}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<AuditCriteriaResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(7m, updated.PotentialPoint);
        Assert.Equal("Updated description", updated.Description);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: the tenant filter would compare against Guid.Empty,
        // so read the row back unfiltered and assert its company stamp explicitly.
        var stored = await db.AuditCriteria.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal(7m, stored.PotentialPoint);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync();
        var criteria = await factory.SeedMasterAsync();
        using var client = CreateAuthorizedClient(factory);
        // Valid body (the validator must pass) - only the route id is unknown.
        var command = new UpdateAuditCriteriaCommand(
            Guid.NewGuid(), category, 0, criteria, 0, null, null, null, null, null, null);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync();
        var criteria = await factory.SeedMasterAsync();
        var rowId = await factory.SeedCriteriaAsync(category, criteria);
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var getResponse = await client.GetAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_SendsNoNotification()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedCategoryAsync();
        var criteria = await factory.SeedMasterAsync();
        var rowId = await factory.SeedCriteriaAsync(category, criteria);
        await factory.SeedFindingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Spec 21.8 names exactly one delete notification in this domain - Finding (AU-03).
        // Criteria delete is not on the list (flagged): no in-app message goes out.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditCriteriaApiFactory(AuditCriteriaRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
