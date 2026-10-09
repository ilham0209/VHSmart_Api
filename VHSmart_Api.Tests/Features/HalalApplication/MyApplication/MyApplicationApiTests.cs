using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class MyApplicationApiTests
{
    private const string Route = "/api/halal-application/my-applications";

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

    private static CreateApplicationCommand CorrectCommand(Guid schemeId) =>
        ApplicationTestData.CorrectSurveyCommand(schemeId);

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: []);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ViewOnlyRole_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: [PermissionAction.View]);
        await factory.SeedDatabaseAsync();
        var schemeId = await factory.ProductSchemeIdAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, CorrectCommand(schemeId), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPermission_ReturnsTheSeededRow()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Sereni Trading Sdn Bhd");
        await factory.SeedApplicationAsync("VHS(PR)/01012026/1");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllApplicationsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload.Data);
        Assert.Equal("VHS(PR)/01012026/1", row.ReferenceNo);
        Assert.Equal("Sereni Trading Sdn Bhd", row.CompanyName);
        Assert.Equal("DRAFT", row.Status);
        Assert.Null(row.HalalExpiryDate);
        Assert.Equal(1, payload.TotalRecords);
    }

    [Fact]
    public async Task GetAll_OtherCompanyToken_SeesNoRows()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedApplicationAsync("VHS(PR)/01012026/1");
        using var client = CreateAuthorizedClient(factory, companyId: Guid.NewGuid());

        var response = await client.GetAsync(Route);
        var payload = await ReadAsync<DataGridResponse<GetAllApplicationsResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload.Data);
        Assert.Equal(0, payload.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidCommand_ReturnsDraftWithTheD09Reference()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var schemes = await factory.SeedAsync(async db =>
            await new GetApplicationSchemesHandler(db)
                .Handle(new GetApplicationSchemesQuery(), CancellationToken.None));
        var scheme = schemes.Single(row => row.Code == "PR");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, CorrectCommand(scheme.Id), Json);
        var payload = await ReadAsync<ApplicationResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Matches(@"^VHS\(PR\)/\d{8}/1$", payload.ReferenceNo);
        Assert.Equal("DRAFT", payload.Status);
        Assert.Equal("New", payload.ApplicationType);
        Assert.Equal(scheme.Id, payload.SchemeId);
        Assert.False(payload.SurveyHandlesProhibited);
    }

    [Fact]
    public async Task Create_WrongSurveyAnswer_Returns422WithTheD08Message()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var schemeId = await factory.ProductSchemeIdAsync();
        var command = new CreateApplicationCommand(
            schemeId, null,
            SurveyReadProcedureManual: false,
            SurveyReadMs1500: true,
            SurveyHandlesProhibited: false,
            SurveyHasIhc: true);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(
            "Your answers do not allow you to proceed with a Halal application.",
            problem.Detail);
    }

    [Fact]
    public async Task Create_MissingScheme_Returns400()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var command = CorrectCommand(Guid.Empty);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, command, Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Scheme is required.", problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task GetSurveyQuestions_ReturnsTheFourQuestions()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/survey-questions");
        var payload = await ReadAsync<SurveyQuestionResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(4, payload.Length);
        Assert.Equal("Q1", payload[0].Code);
        Assert.Equal("Q4", payload[3].Code);
    }

    [Fact]
    public async Task GetSchemes_ReturnsTheNineSeededSchemes()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/schemes");
        var payload = await ReadAsync<ApplicationSchemeOptionResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(9, payload.Length);
        Assert.Equal("PR", payload[0].Code);
        Assert.Contains(payload, scheme => scheme.Code == null);
    }
}
