using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.HalalCertificate;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;
using VHSmart_Api.Tests.Features.HalalApplication.MyApplication;
using static VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.HalalCertificate;

// HTTP layer of the Halal Certificate screen (spec 12.8): the grid + detail permissions,
// the edit save with its 409 UQ answer, the D-22 upload and the streamed download.
public class HalalCertificateApiTests
{
    private const string Route = "/api/halal-application/halal-certificates";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        MyApplicationApiFactory factory,
        Guid? companyId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId, companyId));
        return client;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json);

    private static MultipartFormDataContent DocumentForm(string fileName = "certificate.pdf")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", fileName);
        return content;
    }

    private static async Task<Guid> SeedCertificateRowAsync(
        MyApplicationApiFactory factory,
        string certificateNo = "HAL-2026-0001")
    {
        // The helper may run twice in one test (two certificates of one company): seed the
        // shared company row only once.
        var hasCompany = await factory.SeedAsync(db =>
            db.Companies.AnyAsync(row => row.Id == factory.CompanyId));
        if (!hasCompany)
            await factory.SeedCompanyAsync();

        return await factory.SeedAsync(async db =>
        {
            var applicationId = await SeedApplicationAsync(
                db, factory.CompanyId, status: ApplicationStatus.ApplicationApproved);
            return await SeedCertificateAsync(
                db, factory.CompanyId, applicationId, certificateNo);
        });
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [],
            permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithView_ReturnsTheGrid()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [PermissionAction.View],
            permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<
            DataGridResponse<GetHalalCertificatesResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload.Data);
        Assert.Equal(certificateId, row.Id);
        Assert.Equal("HAL-2026-0001", row.CertificateNumber);
        Assert.Equal("Valid", row.HalalCertificateStatus);
        Assert.Equal(0, row.NoCertificateItem);
    }

    [Fact]
    public async Task GetById_ReturnsTheDetail()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [PermissionAction.View],
            permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{certificateId}");
        var payload = await ReadAsync<HalalCertificateDetailResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(certificateId, payload.Id);
        Assert.Equal("HAL-2026-0001", payload.CertificateNumber);
        Assert.Null(payload.FileName);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task Update_ViewOnlyRole_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [PermissionAction.View],
            permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{certificateId}",
            new UpdateHalalCertificateCommand(certificateId, "HAL-2026-0002", null, null),
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidBody_ReturnsTheUpdatedDetail()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{certificateId}",
            new UpdateHalalCertificateCommand(
                certificateId,
                "HAL-2026-0002",
                new DateOnly(2026, 1, 15),
                new DateOnly(2027, 1, 14)),
            Json);
        var payload = await ReadAsync<HalalCertificateDetailResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("HAL-2026-0002", payload.CertificateNumber);
        Assert.Equal(new DateOnly(2026, 1, 15), payload.IssuedDate);
        Assert.Equal(new DateOnly(2027, 1, 14), payload.ExpiryDate);
    }

    [Fact]
    public async Task Update_ANumberAnotherRowOwns_ReturnsConflict()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        await SeedCertificateRowAsync(factory, certificateNo: "HAL-1");
        var secondId = await SeedCertificateRowAsync(factory, certificateNo: "HAL-2");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{secondId}",
            new UpdateHalalCertificateCommand(secondId, "HAL-1", null, null),
            Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Certificate number already exists.", problem?.Detail);
    }

    [Fact]
    public async Task UploadDocument_ValidForm_StoresTheFileAndDownloadsIt()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var upload = await client.PostAsync(
            $"{Route}/{certificateId}/document", DocumentForm());
        var payload = await ReadAsync<HalalCertificateDetailResponse>(upload);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("certificate.pdf", payload.FileName);

        var download = await client.GetAsync($"{Route}/{certificateId}/document");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", await download.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadDocument_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{certificateId}/document", DocumentForm("payload.exe"));
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.StartsWith("File type is not allowed.", problem?.Detail);
    }

    [Fact]
    public async Task GetById_ForeignToken_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [PermissionAction.View],
            permissionKey: PermissionKeys.HalalApplicationHalalCertificate);
        await factory.SeedDatabaseAsync();
        var certificateId = await SeedCertificateRowAsync(factory);
        using var client = CreateAuthorizedClient(factory, companyId: Guid.NewGuid());

        var response = await client.GetAsync($"{Route}/{certificateId}");
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Halal certificate not found.", problem?.Detail);
    }
}
