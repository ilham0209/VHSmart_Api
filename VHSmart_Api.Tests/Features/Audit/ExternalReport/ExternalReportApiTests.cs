using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class ExternalReportApiTests
{
    private const string Route = "/api/audit/external-report";

    private static readonly Guid ReportRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        ExternalReportApiFactory factory,
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
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new { }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsRowWithJoinedColumns()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllExternalAuditReportsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(reportId, row.Id);
        Assert.Null(row.AuditReferenceNo);
        Assert.Equal("HALAL CO", row.CompanyName);
        Assert.Equal("APP-001", row.CbApplicationNo);
        Assert.Equal("Seri Rasa Factory", row.Premise);
        Assert.Equal(
            "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", row.ReceivedFrom);
        Assert.Equal("Opened", row.Status);
    }

    [Fact]
    public async Task GetAll_OtherCompany_SeesNothing()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        await factory.SeedFullStackAsync();
        using var otherFactory = new ExternalReportApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(otherFactory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllExternalAuditReportsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task GetOptions_ReturnsD10ReferenceData()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<ExternalReportOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal("HALAL CO", options.CompanyName);
        Assert.Contains(
            options.CertificationBodies,
            option => option.Name == "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)");
        Assert.Equal(["New Application"], options.TypeOfApplications);
        Assert.Equal(["Surprise Inspection"], options.TypeOfInspections);
        Assert.Contains(options.Schemes, option => option.Name == "Halal Food");
        Assert.Equal(["Pass", "Fail", "Pending"], options.OverallStatuses);
        var premise = Assert.Single(options.Premises);
        Assert.Equal("Seri Rasa Factory", premise.Name);
        Assert.Equal("STORE01", premise.StoreCode);
    }

    [Fact]
    public async Task GetById_ReturnsDetailWithPremiseLinks()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{reportId}");
        var detail = await response.Content.ReadFromJsonAsync<ExternalAuditReportDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("HALAL-20251013-131025(PM)", detail.CbAuditReferenceNo);
        Assert.Equal("Opened", detail.Status);
        Assert.Equal("Pass", detail.OverallStatus);
        var link = Assert.Single(detail.Premises);
        Assert.Equal("Seri Rasa Factory", link.Name);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidBody_ReturnsOpenedReport()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (cb, scheme, _, _, _, premise, _, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new
        {
            receivedFromCertificationBodyId = cb,
            cbAuditReferenceNo = "HALAL-2026-42",
            typeOfApplication = "New Application",
            typeOfInspection = "Surprise Inspection",
            applicationSchemeId = scheme,
            cbHalalApplicationNo = "APP-042",
            certificationNo = "CERT-042",
            auditDate = "2026-01-15",
            timeIn = "09:30:00",
            timeOut = "13:00:00",
            remarks = "Second visit",
            overallStatus = "Pending",
            premiseIds = new[] { premise }
        }, Json);
        var detail = await response.Content.ReadFromJsonAsync<ExternalAuditReportDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Null(detail.AuditReferenceNo);
        Assert.Equal("Opened", detail.Status);
        Assert.Equal("Pending", detail.OverallStatus);
        Assert.Equal(new DateOnly(2026, 1, 15), detail.AuditDate);
        Assert.Single(detail.Premises);
    }

    [Fact]
    public async Task Create_EmptyBody_ReturnsBadRequest()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForeignPremise_ReturnsBadRequest()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (cb, scheme, _, _, _, _, _, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new
        {
            receivedFromCertificationBodyId = cb,
            cbAuditReferenceNo = "HALAL-2026-43",
            typeOfApplication = "New Application",
            typeOfInspection = "Surprise Inspection",
            applicationSchemeId = scheme,
            cbHalalApplicationNo = (string?)null,
            certificationNo = (string?)null,
            auditDate = "2026-01-15",
            timeIn = "09:30:00",
            timeOut = "13:00:00",
            remarks = (string?)null,
            overallStatus = "Pass",
            premiseIds = new[] { Guid.NewGuid() }
        }, Json);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors.Values.SelectMany(messages => messages),
            message => message == "Premise not found.");
    }

    [Fact]
    public async Task Update_ValidBody_UpdatesReport()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (cb, scheme, _, _, _, premise, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{reportId}", new
        {
            receivedFromCertificationBodyId = cb,
            cbAuditReferenceNo = "HALAL-UPDATED",
            typeOfApplication = "New Application",
            typeOfInspection = "Surprise Inspection",
            applicationSchemeId = scheme,
            cbHalalApplicationNo = "APP-001",
            certificationNo = "CERT-001",
            auditDate = "2026-02-01",
            timeIn = "10:00:00",
            timeOut = "14:00:00",
            remarks = "Revised remarks",
            overallStatus = "Fail",
            premiseIds = new[] { premise }
        }, Json);
        var detail = await response.Content.ReadFromJsonAsync<ExternalAuditReportDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("HALAL-UPDATED", detail.CbAuditReferenceNo);
        Assert.Equal("Fail", detail.OverallStatus);
        Assert.Equal(new DateOnly(2026, 2, 1), detail.AuditDate);
        Assert.Equal("Revised remarks", detail.Remarks);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (cb, scheme, _, _, _, premise, _, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", new
        {
            receivedFromCertificationBodyId = cb,
            cbAuditReferenceNo = "HALAL-2026-44",
            typeOfApplication = "New Application",
            typeOfInspection = "Surprise Inspection",
            applicationSchemeId = scheme,
            cbHalalApplicationNo = (string?)null,
            certificationNo = (string?)null,
            auditDate = "2026-01-15",
            timeIn = "09:30:00",
            timeOut = "13:00:00",
            remarks = (string?)null,
            overallStatus = "Pass",
            premiseIds = new[] { premise }
        }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Valid_ReturnsNoContent()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, ncId, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{reportId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Children go soft-deleted too (AU-06 children-first stance); scope-reads hide them.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        Assert.Equal(
            0,
            await db.ExternalAuditReports.CountAsync(row => row.Id == reportId));
        Assert.Equal(0, await db.NonConformances.CountAsync(row => row.Id == ncId));
        Assert.Equal(
            0,
            await db.ExternalAuditReportAttachments.CountAsync(row => row.Id == attachmentId));
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetNonConformances_ReturnsRowsWithCategory()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, ncId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{reportId}/non-conformances");
        var rows = await response.Content.ReadFromJsonAsync<List<NonConformanceResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rows);
        var nc = Assert.Single(rows);
        Assert.Equal(ncId, nc.Id);
        Assert.Equal("Critical", nc.Category);
        Assert.Equal("Open", nc.Status);
        Assert.Equal("Pest traces found", nc.NonConformanceDetails);
        Assert.Null(nc.CorrectiveActionDetails);
    }

    [Fact]
    public async Task CreateNonConformance_ValidBody_CreatesOpenNc()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, categoryId, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{reportId}/non-conformances",
            new
            {
                itemName = "Seri Rasa Factory",
                detailName = "Chicken Curry",
                reference = "REF-2",
                clause = "Clause 3.1",
                nonConformanceDetails = "Cold storage too warm",
                categoryId,
                remarks = "Second finding"
            },
            Json);
        var nc = await response.Content.ReadFromJsonAsync<NonConformanceResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(nc);
        Assert.Equal("Open", nc.Status);
        Assert.Equal("Cold storage too warm", nc.NonConformanceDetails);
    }

    [Fact]
    public async Task CreateNonConformance_EmptyRemarks_ReturnsBadRequest()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, categoryId, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{reportId}/non-conformances",
            new
            {
                itemName = (string?)null,
                detailName = (string?)null,
                reference = (string?)null,
                clause = (string?)null,
                nonConformanceDetails = (string?)null,
                categoryId,
                remarks = ""
            },
            Json);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors.Values.SelectMany(messages => messages),
            message => message == "Remarks is required.");
    }

    [Fact]
    public async Task UpdateNonConformance_ValidBody_UpdatesRow()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, categoryId, _, _, ncId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/non-conformances/{ncId}",
            new
            {
                itemName = "Kedai Serai Corner",
                detailName = "Nasi Lemak",
                reference = "REF-9",
                clause = "Clause 4.2",
                nonConformanceDetails = "Temperature log missing",
                categoryId,
                remarks = "Updated remarks"
            },
            Json);
        var nc = await response.Content.ReadFromJsonAsync<NonConformanceResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(nc);
        Assert.Equal("Kedai Serai Corner", nc.ItemName);
        Assert.Equal("Temperature log missing", nc.NonConformanceDetails);
    }

    [Fact]
    public async Task DeleteNonConformance_Valid_ReturnsNoContent()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, _, ncId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/non-conformances/{ncId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_Valid_ReturnsStoredFile()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([37, 80, 68, 70]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "extra-report.pdf");
        var response = await client.PostAsync($"{Route}/{reportId}/attachments", form);
        var attachment = await response.Content.ReadFromJsonAsync<
            ExternalAuditReportAttachmentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(attachment);
        Assert.Equal("extra-report.pdf", attachment.FileName);
        Assert.Equal(4, attachment.SizeBytes);
    }

    [Fact]
    public async Task UploadAttachment_DisallowedExtension_ReturnsUnprocessable()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "File", "malware.exe");
        var response = await client.PostAsync($"{Route}/{reportId}/attachments", form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetAttachments_ReturnsRow()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, reportId, _, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{reportId}/attachments");
        var rows = await response.Content.ReadFromJsonAsync<
            List<ExternalAuditReportAttachmentResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rows);
        var attachment = Assert.Single(rows);
        Assert.Equal(attachmentId, attachment.Id);
        Assert.Equal("report.pdf", attachment.FileName);
    }

    [Fact]
    public async Task DeleteAttachment_Valid_ReturnsNoContent()
    {
        using var factory = new ExternalReportApiFactory(ReportRoleId);
        var (_, _, _, _, _, _, _, _, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
