using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.GroupAuditor;

public class GroupAuditorApiTests
{
    private const string Route = "/api/audit/group-auditor";

    private static readonly Guid GroupAuditorRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client needs
    // the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(GroupAuditorApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(GroupAuditorRoleId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGroupAuditorCommand(
            "TEAM", GroupAuditorStatus.Active, Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateGroupAuditorCommand(
            Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        var response = await client.PutAsJsonAsync($"{Route}/{command.Id}", command, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_EmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllGroupAuditorsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task GetAll_WithRow_ReturnsSpecColumnContract()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync("LEAD ONE");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        db.GroupAuditors.Add(new GroupAuditorEntity
        {
            CompanyId = factory.CompanyId,
            Name = "TEAM ALPHA",
            Status = GroupAuditorStatus.Active,
            ChecklistId = checklist,
            LeaderUserId = leader
        });
        db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = factory.CompanyId,
            GroupAuditorId = db.GroupAuditors.Local.First().Id,
            UserId = leader
        });
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllGroupAuditorsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal("TEAM ALPHA", row.GroupName);
        Assert.Equal("LEAD ONE", row.GroupLeaderName);
        Assert.Equal("LEAD ONE", row.AuditorName);
        Assert.Equal(GroupAuditorStatus.Active, row.Status);

        // CodingRules 11: the wire carries camelCase props and the enum as its spec string.
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var first = document.RootElement.GetProperty("data")[0];
        Assert.Equal("TEAM ALPHA", first.GetProperty("groupName").GetString());
        Assert.Equal("Active", first.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsHeaderAndListShowsTheRow()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync("LEAD ONE");
        var second = await factory.SeedAuditorAsync("AUDITOR TWO");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGroupAuditorCommand(
            "TEAM ALPHA", GroupAuditorStatus.Active, checklist, leader, [leader, second]);

        var createResponse = await client.PostAsJsonAsync(Route, command, Json);
        var created = await createResponse.Content.ReadFromJsonAsync<GroupAuditorResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(2, created.MemberIds.Count);
        Assert.Equal(leader, created.LeaderUserId);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllGroupAuditorsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal("TEAM ALPHA", row.GroupName);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGroupAuditorCommand(
            "", GroupAuditorStatus.Active, checklist, leader, [leader]);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForeignChecklist_ReturnsBadRequest()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var leader = await factory.SeedAuditorAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGroupAuditorCommand(
            "TEAM", GroupAuditorStatus.Active, Guid.NewGuid(), leader, [leader]);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_LeaderNotAmongMembers_ReturnsBadRequest()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync("LEAD ONE");
        var second = await factory.SeedAuditorAsync("AUDITOR TWO");
        using var client = CreateAuthorizedClient(factory);
        var command = new CreateGroupAuditorCommand(
            "TEAM", GroupAuditorStatus.Active, checklist, leader, [second]);

        var response = await client.PostAsJsonAsync(Route, command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Valid_ReturnsDetail()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync("LEAD ONE");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var group = new GroupAuditorEntity
        {
            CompanyId = factory.CompanyId,
            Name = "TEAM ALPHA",
            Status = GroupAuditorStatus.Inactive,
            ChecklistId = checklist,
            LeaderUserId = leader
        };
        db.GroupAuditors.Add(group);
        db.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = factory.CompanyId,
            GroupAuditorId = group.Id,
            UserId = leader
        });
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{group.Id}");
        var detail = await response.Content.ReadFromJsonAsync<GroupAuditorResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("TEAM ALPHA", detail.Name);
        Assert.Equal(GroupAuditorStatus.Inactive, detail.Status);
        Assert.Equal([leader], detail.MemberIds);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidCommand_ReturnsOkAndPersists()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync("LEAD ONE");
        using var seedScope = factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var group = new GroupAuditorEntity
        {
            CompanyId = factory.CompanyId,
            Name = "TEAM OLD",
            Status = GroupAuditorStatus.Active,
            ChecklistId = checklist,
            LeaderUserId = leader
        };
        seedDb.GroupAuditors.Add(group);
        seedDb.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = factory.CompanyId,
            GroupAuditorId = group.Id,
            UserId = leader
        });
        await seedDb.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);
        var command = new UpdateGroupAuditorCommand(
            group.Id, "TEAM NEW", GroupAuditorStatus.Inactive, checklist, leader, [leader]);

        var response = await client.PutAsJsonAsync($"{Route}/{group.Id}", command, Json);
        var updated = await response.Content.ReadFromJsonAsync<GroupAuditorResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("TEAM NEW", updated.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == group.Id);
        Assert.Equal("TEAM NEW", stored.Name);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync();
        using var client = CreateAuthorizedClient(factory);
        // Valid body (own checklist + own auditor) so the validator passes and the handler
        // is the one answering 404.
        var command = new UpdateGroupAuditorCommand(
            Guid.NewGuid(), "TEAM", GroupAuditorStatus.Active, checklist, leader, [leader]);

        var response = await client.PutAsJsonAsync($"{Route}/{command.Id}", command, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ValidCommand_ReturnsNoContentAndSoftDeletes()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        var checklist = await factory.SeedChecklistAsync();
        var leader = await factory.SeedAuditorAsync();
        using var seedScope = factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var group = new GroupAuditorEntity
        {
            CompanyId = factory.CompanyId,
            Name = "TEAM ALPHA",
            Status = GroupAuditorStatus.Active,
            ChecklistId = checklist,
            LeaderUserId = leader
        };
        seedDb.GroupAuditors.Add(group);
        seedDb.GroupAuditorMembers.Add(new GroupAuditorMemberEntity
        {
            CompanyId = factory.CompanyId,
            GroupAuditorId = group.Id,
            UserId = leader
        });
        await seedDb.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{group.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.GroupAuditors.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == group.Id);
        Assert.True(stored.IsDeleted);
        Assert.True((await db.GroupAuditorMembers.IgnoreQueryFilters()
            .SingleAsync(row => row.GroupAuditorId == group.Id)).IsDeleted);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new GroupAuditorApiFactory(GroupAuditorRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
