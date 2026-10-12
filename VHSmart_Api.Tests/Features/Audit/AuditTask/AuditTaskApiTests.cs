using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.AuditTask;

public class AuditTaskApiTests
{
    private const string Route = "/api/audit/audit-task";

    private static readonly Guid TaskRoleId = RoleSeedData.VhSmartAdminRoleId;

    // The API serializes enums as their spec strings (CodingRules 11); the test client
    // needs the matching converter to read them back.
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        AuditTaskApiFactory factory,
        Guid? memberUserId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", factory.CreateToken(TaskRoleId, memberUserId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Start_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync($"{Route}/{Guid.NewGuid()}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Member_SeesOwnTaskWithSpecColumns()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllAuditTasksResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(taskId, row.Id);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", row.AuditReferenceNo);
        Assert.Equal(new DateOnly(2026, 11, 1), row.DateAssigned);
        Assert.Equal("PLANNER ONE", row.AssignedBy);
        Assert.Equal(new DateOnly(2026, 11, 18), row.ScheduleDate);
        Assert.Equal("Seri Rasa Factory", row.PremiseAudited);
        Assert.Null(row.ActualAuditDate);
        Assert.Null(row.ActualTimeIn);
        Assert.Null(row.ActualTimeOut);
        Assert.Null(row.ReportingTo);
        Assert.Equal(AuditTaskStatus.NotStarted, row.Status);
    }

    [Fact]
    public async Task GetAll_NonMember_SeesNothing()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        await factory.SeedTaskAsync();
        // The token belongs to a user who is no member of the plan's group.
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllAuditTasksResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task GetById_ReturnsHeaderWithGroupAndAddress()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.GetAsync($"{Route}/{taskId}");
        var header = await response.Content.ReadFromJsonAsync<GetAuditTaskByIdResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(header);
        Assert.Equal(taskId, header.Id);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", header.AuditReferenceNo);
        Assert.Equal(new DateOnly(2026, 11, 18), header.ScheduleDate);
        Assert.Equal("Seri Rasa Factory", header.PremiseName);
        Assert.Equal("1 Jalan Verify, Taman Industri", header.PremiseAddress);
        Assert.Equal("TEAM ALPHA", header.GroupName);
        Assert.Equal(new[] { "AUDITOR ONE" }, header.GroupMemberNames);
        Assert.Equal("AUDITOR ONE", header.GroupLeaderName);
        Assert.Equal("checklist tech", header.ChecklistName);
        Assert.Equal("PLANNER ONE", header.AssignedByName);
        Assert.Equal(new DateOnly(2026, 11, 1), header.DateAssigned);
        Assert.Equal(0m, header.AuditScore);
        Assert.Equal(0m, header.FinalReviewScore);
        Assert.Equal(AuditTaskStatus.NotStarted, header.Status);
    }

    [Fact]
    public async Task GetById_UnknownTask_ReturnsNotFound()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (_, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_BeforeStart_ReturnsEmptySnapshot()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.GetAsync($"{Route}/{taskId}/items");
        var items = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<GetAuditTaskItemsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Start_SnapshotsChecklist_AndItemsShowTheFrozenColumns()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        var startResponse = await client.PostAsync($"{Route}/{taskId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, startResponse.StatusCode);

        var itemsResponse = await client.GetAsync($"{Route}/{taskId}/items");
        var items = await itemsResponse.Content.ReadFromJsonAsync<
            IReadOnlyList<GetAuditTaskItemsResponse>>(Json);

        Assert.NotNull(items);
        Assert.Equal(3, items.Count);
        Assert.Equal("PEST CONTROL", items[0].Category);
        Assert.Equal("Clean floor", items[0].Criteria);
        Assert.Equal("Detailed records kept", items[0].SubCriteria);
        Assert.Equal("ISO 4.5", items[0].Reference);
        Assert.Equal(2m, items[0].PotentialPoint);
        Assert.Null(items[0].ActualPoint);
        Assert.Equal(3m, items[1].PotentialPoint);
        Assert.Equal("CLEANING", items[2].Category);
        Assert.Equal(1m, items[2].PotentialPoint);

        var header = await (await client.GetAsync($"{Route}/{taskId}"))
            .Content.ReadFromJsonAsync<GetAuditTaskByIdResponse>(Json);
        Assert.NotNull(header);
        Assert.Equal(AuditTaskStatus.InProgress, header.Status);
        Assert.NotNull(header.StartAuditDate);
        Assert.NotNull(header.TimeIn);
    }

    [Fact]
    public async Task Start_Twice_ReturnsConflict()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync(AuditTaskStatus.InProgress);
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.PostAsync($"{Route}/{taskId}/start", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_AnswersTheRow()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);
        var start = await client.PostAsync($"{Route}/{taskId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);
        var items = await (await client.GetAsync($"{Route}/{taskId}/items"))
            .Content.ReadFromJsonAsync<IReadOnlyList<GetAuditTaskItemsResponse>>(Json);
        Assert.NotNull(items);

        var command = new UpdateAuditTaskItemCommand(items[0].Id, 2m, false, "Found traces.");
        var response = await client.PutAsJsonAsync(
            $"{Route}/items/{items[0].Id}", command, Json);
        var updated = await (await client.GetAsync($"{Route}/{taskId}/items"))
            .Content.ReadFromJsonAsync<IReadOnlyList<GetAuditTaskItemsResponse>>(Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(2m, updated[0].ActualPoint);
        Assert.False(updated[0].IsConforming);
        Assert.Equal("Found traces.", updated[0].Remarks);
    }

    [Fact]
    public async Task UpdateItem_BeforeStart_ReturnsConflict()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var item = new AuditTaskItemEntity
        {
            CompanyId = factory.CompanyId,
            AuditTaskId = taskId,
            Category = "PEST CONTROL",
            CategorySequence = 1,
            Criteria = "Clean floor",
            CriteriaSequence = 1,
            PotentialPoint = 2m
        };
        db.AuditTaskItems.Add(item);
        await db.SaveChangesAsync();

        var response = await client.PutAsJsonAsync(
            $"{Route}/items/{item.Id}",
            new UpdateAuditTaskItemCommand(item.Id, 1m, null, null),
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_CompletesTheAudit_AndLocksFurtherEdits()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync();
        using var client = CreateAuthorizedClient(factory, member);
        var start = await client.PostAsync($"{Route}/{taskId}/start", null);
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);

        var items = await (await client.GetAsync($"{Route}/{taskId}/items"))
            .Content.ReadFromJsonAsync<IReadOnlyList<GetAuditTaskItemsResponse>>(Json);
        Assert.NotNull(items);
        // Potential 6, actual 2 + 3 + 1 = 6 -> 100.00 (D-06).
        var actuals = new[] { 2m, 3m, 1m };
        for (var index = 0; index < items.Count; index++)
        {
            var answer = await client.PutAsJsonAsync(
                $"{Route}/items/{items[index].Id}",
                new UpdateAuditTaskItemCommand(items[index].Id, actuals[index], true, null),
                Json);
            Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        }

        var submit = await client.PostAsJsonAsync(
            $"{Route}/{taskId}/submit",
            new SubmitAuditTaskCommand(
                taskId, "All good.", "Madam Tan", "Restaurant Manager"),
            Json);
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

        var header = await (await client.GetAsync($"{Route}/{taskId}"))
            .Content.ReadFromJsonAsync<GetAuditTaskByIdResponse>(Json);
        Assert.NotNull(header);
        Assert.Equal(AuditTaskStatus.Completed, header.Status);
        Assert.Equal(100.00m, header.AuditScore);
        Assert.NotNull(header.EndAuditDate);
        Assert.NotNull(header.TimeOut);

        // Locked: answering again is a 409.
        var lateAnswer = await client.PutAsJsonAsync(
            $"{Route}/items/{items[0].Id}",
            new UpdateAuditTaskItemCommand(items[0].Id, 1m, null, null),
            Json);
        Assert.Equal(HttpStatusCode.Conflict, lateAnswer.StatusCode);
    }

    [Fact]
    public async Task Submit_MissingAcknowledgementFields_ReturnsBadRequest()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync(AuditTaskStatus.InProgress);
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{taskId}/submit",
            new SubmitAuditTaskCommand(taskId, "", "Madam Tan", "Manager"),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FinalPoint_AfterSubmit_RecomputesFinalReviewScore()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync(AuditTaskStatus.Completed);
        using var client = CreateAuthorizedClient(factory, member);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var item = new AuditTaskItemEntity
        {
            CompanyId = factory.CompanyId,
            AuditTaskId = taskId,
            Category = "PEST CONTROL",
            CategorySequence = 1,
            Criteria = "Clean floor",
            CriteriaSequence = 1,
            PotentialPoint = 2m,
            ActualPoint = 2m
        };
        db.AuditTaskItems.Add(item);
        await db.SaveChangesAsync();

        var response = await client.PutAsJsonAsync(
            $"{Route}/{taskId}/items/{item.Id}/final-point",
            new UpdateAuditTaskFinalPointCommand(taskId, item.Id, 1m),
            Json);
        var header = await (await client.GetAsync($"{Route}/{taskId}"))
            .Content.ReadFromJsonAsync<GetAuditTaskByIdResponse>(Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(header);
        // 1 / 2 * 100 = 50.00 (D-06).
        Assert.Equal(50.00m, header.FinalReviewScore);
    }

    [Fact]
    public async Task FinalPoint_BeforeCompletion_ReturnsConflict()
    {
        using var factory = new AuditTaskApiFactory(TaskRoleId);
        var (taskId, member, _) = await factory.SeedTaskAsync(AuditTaskStatus.InProgress);
        using var client = CreateAuthorizedClient(factory, member);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{taskId}/items/{Guid.NewGuid()}/final-point",
            new UpdateAuditTaskFinalPointCommand(taskId, Guid.NewGuid(), 1m),
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
