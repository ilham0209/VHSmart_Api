using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;
using VHSmart_Api.Tests.Features.HalalApplication.MyApplication;
using static VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;

// HTTP layer of the Certificate Item screen (spec 12.8): the grid permission, the "CLICK TO
// ADD" PUT with its validator answers, the 404 for foreign items and the 403 for View-only.
public class CertificateItemApiTests
{
    private const string Route = "/api/halal-application/certificate-items";

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

    private static async Task<Guid> SeedItemAsync(
        MyApplicationApiFactory factory,
        string itemName = "Santan Kicap")
    {
        await factory.SeedCompanyAsync();
        return await factory.SeedAsync(async db =>
        {
            var applicationId = await SeedApplicationAsync(
                db, factory.CompanyId, status: ApplicationStatus.ApplicationApproved);
            return await SeedCertificateItemAsync(
                db, factory.CompanyId, applicationId, itemName);
        });
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [],
            permissionKey: PermissionKeys.HalalApplicationCertificateItem);
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
            permissionKey: PermissionKeys.HalalApplicationCertificateItem);
        await factory.SeedDatabaseAsync();
        var itemId = await SeedItemAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetCertificateItemsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload.Data);
        Assert.Equal(itemId, row.Id);
        Assert.Equal("Santan Kicap", row.CertificateItem);
        Assert.Equal("Sereni Trading Sdn Bhd", row.CompanyName);
        Assert.Null(row.CertificateNumber);
    }

    [Fact]
    public async Task AddCertificateNumber_ViewOnlyRole_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId,
            grantedActions: [PermissionAction.View],
            permissionKey: PermissionKeys.HalalApplicationCertificateItem);
        await factory.SeedDatabaseAsync();
        var itemId = await SeedItemAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{itemId}/certificate-number",
            new AddCertificateNumberToItemCommand(itemId, "HAL-2026-0001"),
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCertificateNumber_ValidBody_ReturnsTheLinkedCertificate()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationCertificateItem);
        await factory.SeedDatabaseAsync();
        var itemId = await SeedItemAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{itemId}/certificate-number",
            new AddCertificateNumberToItemCommand(itemId, "HAL-2026-0001"),
            Json);
        var payload = await ReadAsync<AddCertificateNumberResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(itemId, payload.ItemId);
        Assert.Equal("HAL-2026-0001", payload.CertificateNumber);
        Assert.NotEqual(Guid.Empty, payload.HalalCertificateId);
    }

    [Fact]
    public async Task AddCertificateNumber_EmptyNumber_ReturnsBadRequest()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationCertificateItem);
        await factory.SeedDatabaseAsync();
        var itemId = await SeedItemAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{itemId}/certificate-number",
            new AddCertificateNumberToItemCommand(itemId, ""),
            Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            "Certificate number is required.",
            problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task AddCertificateNumber_ForeignToken_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, permissionKey: PermissionKeys.HalalApplicationCertificateItem);
        await factory.SeedDatabaseAsync();
        var itemId = await SeedItemAsync(factory);
        using var client = CreateAuthorizedClient(factory, companyId: Guid.NewGuid());

        var response = await client.PutAsJsonAsync(
            $"{Route}/{itemId}/certificate-number",
            new AddCertificateNumberToItemCommand(itemId, "HAL-2026-0001"),
            Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Certificate item not found.", problem?.Detail);
    }
}
