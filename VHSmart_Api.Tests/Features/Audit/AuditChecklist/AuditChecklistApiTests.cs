using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.AuditChecklist;

public class AuditChecklistApiTests
{
    private const string Route = "/api/audit/audit-checklist";

    private static readonly Guid ChecklistRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(AuditChecklistApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(ChecklistRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditChecklistCommand(Guid.NewGuid(), "Checklist", null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditChecklistsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsHeaderWithoutCriteriaAndListShowsTheRow()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync("Internal Supplier");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditChecklistCommand(
            category, "Checklist testing 18 Nov 25", "Do not use");

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<AuditChecklistResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        // Two-step save (spec 14.6 [MANUAL]): step 1 persists the header only.
        Assert.Empty(created.CriteriaIds);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllAuditChecklistsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Internal Supplier", row.ChecklistCategory);
        Assert.Equal("Checklist testing 18 Nov 25", row.Name);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditChecklistCommand(category, string.Empty, null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsBadRequest()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditChecklistCommand(Guid.NewGuid(), "Checklist", null);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        // Validation runs BEFORE the handler (the house pipeline).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingRow_ReturnsTheDetail()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync("Syariah");
        var criteria = await factory.SeedCriteriaAsync();
        var rowId = await factory.SeedChecklistAsync(
            category, "checklist syariah 2.0", "Do not use");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.AuditChecklistCriteria.Add(new AuditChecklistCriteriaEntity
        {
            CompanyId = factory.CompanyId,
            ChecklistId = rowId,
            AuditCriteriaId = criteria
        });
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{rowId}");
        var row = await response.Content.ReadFromJsonAsync<AuditChecklistResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal("checklist syariah 2.0", row.Name);
        Assert.Equal([criteria], row.CriteriaIds);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_TwoStepSave_PersistsHeaderAndCriteriaSelection()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        var criteria = await factory.SeedCriteriaAsync();
        var rowId = await factory.SeedChecklistAsync(category, "Old name");
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditChecklistCommand(
            rowId, category, "Checklist testing 18 Nov 25", "Updated", [criteria]);

        var response = await client.PutAsJsonAsync($"{Route}/{rowId}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<AuditChecklistResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Checklist testing 18 Nov 25", updated.Name);
        Assert.Equal([criteria], updated.CriteriaIds);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No HTTP context in this scope: read back unfiltered and assert the stamp.
        var stored = await db.AuditChecklists.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Checklist testing 18 Nov 25", stored.Name);
        Assert.Equal(factory.CompanyId, stored.CompanyId);
        var link = await db.AuditChecklistCriteria.AsNoTracking().IgnoreQueryFilters().SingleAsync();
        Assert.Equal(rowId, link.ChecklistId);
        Assert.Equal(criteria, link.AuditCriteriaId);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        using var client = CreateAuthorizedClient(factory);
        // Valid body (the validator must pass) - only the route id is unknown.
        var command = new UpdateAuditChecklistCommand(
            Guid.NewGuid(), category, "Checklist", null, null);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChecklistUsedByAPlan_ReturnsConflict()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        var rowId = await factory.SeedChecklistAsync(category);
        await factory.SeedPlanAsync(rowId);
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateAuditChecklistCommand(
            rowId, category, "Changed", null, null);

        var response = await client.PutAsJsonAsync($"{Route}/{rowId}", command, Json);

        // Computed lock (spec 14.6, Database.md 12): 409, not 403 - the checklist exists
        // and is readable, only the state refuses the write.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        var rowId = await factory.SeedChecklistAsync(category);
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var getResponse = await client.GetAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ChecklistUsedByAPlan_ReturnsConflict()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        var rowId = await factory.SeedChecklistAsync(category);
        await factory.SeedPlanAsync(rowId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_SendsNoNotification()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync();
        var rowId = await factory.SeedChecklistAsync(category);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Spec 21.8 names exactly one delete notification in this domain - Finding (AU-03).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Options_ReturnsTheChecklistCategories()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        var category = await factory.SeedChecklistCategoryAsync("Internal Supplier");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<AuditChecklistOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal(category, Assert.Single(options.Categories).Id);
        Assert.Equal("Internal Supplier", options.Categories[0].Name);
    }

    [Fact]
    public async Task CriteriaOptions_ReturnsThePagedSelectionGrid()
    {
        using var factory = new AuditChecklistApiFactory(ChecklistRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCriteriaAsync("Pest control schedule kept");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/criteria-options");
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<AuditChecklistCriteriaOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("Pest Control", row.Category);
        Assert.Equal("Pest control schedule kept", row.Criteria);
    }
}
