using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.AuditPlanning;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

public class AuditPlanningApiTests
{
    private const string Route = "/api/audit/audit-planning";

    private static readonly Guid PlanningRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static HttpClient CreateAuthorizedClient(AuditPlanningApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(PlanningRoleId));
        return client;
    }

    [Fact]
    public async Task GetCalendar_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{Route}/calendar?view=month&date=2026-11-18");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendar_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/calendar?view=month&date=2026-11-18");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid());

        var response = await client.PostAsJsonAsync(Route, command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/download?view=month&date=2026-11-18");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCalendar_Authenticated_EmptyCompany_ReturnsEmptyItems()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/calendar?view=month&date=2026-11-18");
        var calendar = await response.Content.ReadFromJsonAsync<AuditPlanningCalendarResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(calendar);
        Assert.Empty(calendar.Items);
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.From);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsReferenceAndCalendarShowsTheBar()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (purpose, type, category) = await factory.GetDropdownIdsAsync();
        var (premise, group, _) = await factory.SeedPlanningTargetAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            purpose, type, category, premise, new DateOnly(2026, 11, 18), group);

        var createResponse = await client.PostAsJsonAsync(Route, command);
        var created = await createResponse.Content.ReadFromJsonAsync<AuditPlanResponse>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", created.AuditReferenceNo);
        Assert.NotEqual(Guid.Empty, created.AuditTaskId);

        var calendarResponse = await client.GetAsync($"{Route}/calendar?view=month&date=2026-11-18");
        var calendar = await calendarResponse.Content.ReadFromJsonAsync<AuditPlanningCalendarResponse>();

        Assert.Equal(HttpStatusCode.OK, calendarResponse.StatusCode);
        Assert.NotNull(calendar);
        var bar = Assert.Single(calendar.Items);
        Assert.Equal("Seri Rasa Factory", bar.PremiseName);
        Assert.Equal("STORE01", bar.StoreCode);
        Assert.Equal("TEAM ALPHA", bar.GroupName);

        // CodingRules 11 on the wire: camelCase JSON.
        var json = await calendarResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var first = document.RootElement.GetProperty("items")[0];
        Assert.Equal("TEAM ALPHA", first.GetProperty("groupName").GetString());
    }

    [Fact]
    public async Task Create_MissingPurpose_ReturnsBadRequest()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (_, type, category) = await factory.GetDropdownIdsAsync();
        var (premise, group, _) = await factory.SeedPlanningTargetAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            Guid.NewGuid(), type, category, premise, new DateOnly(2026, 11, 18), group);

        var response = await client.PostAsJsonAsync(Route, command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InactiveGroup_ReturnsBadRequest()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (purpose, type, category) = await factory.GetDropdownIdsAsync();
        var (premise, groupId, _) = await factory.SeedPlanningTargetAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
            var group = await db.GroupAuditors.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == groupId);
            group.Status = GroupAuditorStatus.Inactive;
            await db.SaveChangesAsync();
        }
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            purpose, type, category, premise, new DateOnly(2026, 11, 18), groupId);

        var response = await client.PostAsJsonAsync(Route, command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_PremiseWithoutBrandPrefix_ReturnsUnprocessableEntity()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (purpose, type, category) = await factory.GetDropdownIdsAsync();
        var (premiseId, group, _) = await factory.SeedPlanningTargetAsync();
        Guid barePremise;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
            var premise = await db.Premises.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == premiseId);
            premise.BrandId = null;
            await db.SaveChangesAsync();
            barePremise = premise.Id;
        }
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            purpose, type, category, barePremise, new DateOnly(2026, 11, 18), group);

        var response = await client.PostAsJsonAsync(Route, command);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForeignPremise_ReturnsBadRequest()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (purpose, type, category) = await factory.GetDropdownIdsAsync();
        var (_, group, _) = await factory.SeedPlanningTargetAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            purpose, type, category, Guid.NewGuid(), new DateOnly(2026, 11, 18), group);

        var response = await client.PostAsJsonAsync(Route, command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOptions_ReturnsThePickers()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        await factory.SeedPlanningTargetAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<AuditPlanningOptionsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal("Routine Audit", Assert.Single(options.Purposes).Name);
        Assert.Equal("Internal", Assert.Single(options.Types).Name);
        // The checklist's own "Technical Category" is a second row of the same pair - the
        // Category picker legitimately shows both.
        Assert.Contains(options.Categories, row => row.Name == "PREMISE");
        var premise = Assert.Single(options.Premises);
        Assert.Equal("Seri Rasa Factory", premise.Name);
        var group = Assert.Single(options.Groups);
        Assert.Equal("TEAM ALPHA", group.Name);
        Assert.Equal("Technical Category - checklist tech", group.ChecklistLabel);
    }

    [Fact]
    public async Task Download_ReturnsCsvFileWithDisposition()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedDropdownsAsync();
        var (purpose, type, category) = await factory.GetDropdownIdsAsync();
        var (premise, group, _) = await factory.SeedPlanningTargetAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateAuditPlanCommand(
            purpose, type, category, premise, new DateOnly(2026, 11, 18), group);
        await client.PostAsJsonAsync(Route, command);

        var response = await client.GetAsync($"{Route}/download?view=day&date=2026-11-18");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "audit-plan-day-2026-11-18.csv",
            response.Content.Headers.ContentDisposition?.FileName);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("SRN/STORE01/18-11-2026(1)", csv);
        Assert.Contains("Seri Rasa Factory", csv);
    }

    [Fact]
    public async Task Download_EmptyRange_ReturnsOnlyHeader()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/download?view=month&date=2026-11-18");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = csv.TrimEnd().Split("\r\n");
        Assert.Single(lines);
        Assert.StartsWith("Audit Reference No,", lines[0]);
    }

    [Fact]
    public async Task Download_UnknownView_ReturnsBadRequest()
    {
        using var factory = new AuditPlanningApiFactory(PlanningRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/download?view=quarter&date=2026-11-18");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
