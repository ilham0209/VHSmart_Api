using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.NonConformanceList;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

public class NonConformanceApiTests
{
    private const string Route = "/api/audit/non-conformance";

    private static readonly Guid ReportRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        NonConformanceApiFactory factory,
        Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", factory.CreateToken(ReportRoleId, userId));
        return client;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveCorrectiveAction_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Fixed."), "Details");
        var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/corrective-action", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Review_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}/review",
            new { comment = "c", statusSet = "Closed" },
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsRowsWithCompanyName()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, externalNcId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllNonConformancesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(3, grid.TotalRecords);
        var internalRow = grid.Data.Single(row => row.Id == internalNcId);
        Assert.Equal("Internal", internalRow.Source);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", internalRow.AuditReferenceNo);
        Assert.Equal("HALAL CO", internalRow.CompanyName);
        Assert.Equal("Open", internalRow.Status);
        var externalRow = grid.Data.Single(row => row.Id == externalNcId);
        Assert.Equal("External", externalRow.Source);
    }

    [Fact]
    public async Task GetAll_FinalOnly_ReturnsFinalizedOnly()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, _, _) = await factory.SeedFullStackAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        // No ambient JWT user in a direct scope, so the tenant filter would hide the row.
        var nc = await db.NonConformances.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == internalNcId);
        nc.IsFinal = true;
        await db.SaveChangesAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}?finalOnly=true");
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllNonConformancesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(internalNcId, Assert.Single(grid.Data).Id);
    }

    [Fact]
    public async Task GetAll_OtherCompany_SeesNothing()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        await factory.SeedFullStackAsync();
        using var otherFactory = new NonConformanceApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(otherFactory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllNonConformancesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithCategory()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, _, externalNcId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{externalNcId}");
        var detail = await response.Content.ReadFromJsonAsync<NonConformanceDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("External", detail.Source);
        Assert.Equal("Critical", detail.Category);
        Assert.Equal("Pest traces found", detail.NonConformanceDetails);
        Assert.Equal(0, detail.LatestVersion);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveCorrectiveAction_MultipartWithFile_ReturnsEntryAndSubmitsNc()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Deep clean weekly."), "Details");
        var file = new ByteArrayContent([37, 80, 68, 70]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "evidence.pdf");
        var response = await client.PostAsync(
            $"{Route}/{internalNcId}/corrective-action", form);
        var entry = await response.Content.ReadFromJsonAsync<CorrectiveActionEntryResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal("Auditee", entry.FilledBy);
        Assert.Equal(1, entry.Version);
        Assert.Equal("evidence.pdf", entry.DocumentName);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var nc = await db.NonConformances.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == internalNcId);
        Assert.Equal("CorrectiveActionSubmitted", nc.Status);
    }

    [Fact]
    public async Task SaveCorrectiveAction_EmptyDetails_ReturnsBadRequest()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(""), "Details");
        var response = await client.PostAsync(
            $"{Route}/{internalNcId}/corrective-action", form);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors.Values.SelectMany(messages => messages),
            message => message == "Corrective Action Details is required.");
    }

    [Fact]
    public async Task SaveCorrectiveAction_OnClosedNc_ReturnsConflict()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, _, _, closedNcId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Too late."), "Details");
        var response = await client.PostAsync(
            $"{Route}/{closedNcId}/corrective-action", form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Review_BeforeSubmission_ReturnsConflict()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, _, externalNcId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{externalNcId}/review",
            new { comment = "c", statusSet = "Closed" },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Review_EmptyComment_ReturnsBadRequest()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("Deep clean weekly."), "Details");
            var save = await client.PostAsync(
                $"{Route}/{internalNcId}/corrective-action", form);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        }

        var response = await client.PutAsJsonAsync(
            $"{Route}/{internalNcId}/review",
            new { comment = "", statusSet = "Closed" },
            Json);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors.Values.SelectMany(messages => messages),
            message => message == "Comment is required.");
    }

    [Fact]
    public async Task ReviewFlow_CloseThenHistory_ShowsBothEntries()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, _, _, internalNcId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("Deep clean weekly."), "Details");
            var save = await client.PostAsync(
                $"{Route}/{internalNcId}/corrective-action", form);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        }

        var review = await client.PutAsJsonAsync(
            $"{Route}/{internalNcId}/review",
            new { comment = "Verified on site.", statusSet = "Closed" },
            Json);
        var entry = await review.Content.ReadFromJsonAsync<CorrectiveActionEntryResponse>(Json);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal("Auditor", entry.FilledBy);

        var historyResponse = await client.GetAsync($"{Route}/{internalNcId}/corrective-action-history");
        var rows = await historyResponse.Content.ReadFromJsonAsync<
            List<CorrectiveActionHistoryResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(rows);
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Auditee", "Auditor"], rows.Select(row => row.Action));
        Assert.Equal([1, 2], rows.Select(row => row.Version));
        Assert.Equal("Deep clean weekly.", rows[0].Remarks);
        Assert.Equal("Verified on site.", rows[1].Remarks);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var nc = await db.NonConformances.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == internalNcId);
        Assert.Equal("Closed", nc.Status);
        Assert.NotNull(nc.ClosedAt);
        Assert.NotNull(nc.ClosedBy);
    }

    [Fact]
    public async Task History_UnknownNc_ReturnsNotFound()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/corrective-action-history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuditTaskIdFilter_ReturnsThatTasksInternals()
    {
        using var factory = new NonConformanceApiFactory(ReportRoleId);
        var (_, taskId, _, internalNcId, externalNcId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}?auditTaskId={taskId}");
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllNonConformancesResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(internalNcId, Assert.Single(grid.Data).Id);
        Assert.DoesNotContain(grid.Data, row => row.Id == externalNcId);
    }
}
