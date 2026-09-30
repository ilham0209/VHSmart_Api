using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class SupportingDocumentApiTests
{
    private const string Route = "/api/admin/supporting-documents";

    private const string SequenceExistsMessage = "Data document sequence exist. Please check the existing data.";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(SupportingDocumentsApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId));
        return client;
    }

    // The create/update form is multipart: ForView, DocumentType, DocumentSequence,
    // IsMandatory, Description and - for the SOP views - the template file (spec 5.5). A null
    // value means the field is left out.
    private static MultipartFormDataContent Form(
        string? forView = "SopHas",
        string documentType = "Halal Procedure",
        string? documentSequence = "1",
        string isMandatory = "false",
        string? description = "SOP template",
        string? fileName = null)
    {
        var content = new MultipartFormDataContent();
        if (forView is not null)
            content.Add(new StringContent(forView), "ForView");
        content.Add(new StringContent(documentType), "DocumentType");
        if (documentSequence is not null)
            content.Add(new StringContent(documentSequence), "DocumentSequence");
        content.Add(new StringContent(isMandatory), "IsMandatory");
        if (description is not null)
            content.Add(new StringContent(description), "Description");
        if (fileName is not null)
        {
            var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "Template", fileName);
        }

        return content;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(Route, Form(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedEmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content
            .ReadFromJsonAsync<DataGridResponse<GetAllSupportingDocumentsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Create_ValidForm_ThenListDetailAndTemplateShowIt()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var createResponse = await client.PostAsync(
            Route, Form(fileName: "sop-has.pdf"), CancellationToken.None);
        var created = await createResponse.Content
            .ReadFromJsonAsync<SupportingDocumentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Halal Procedure", created.DocumentType);
        Assert.Equal("sop-has.pdf", created.TemplateFileName);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content
            .ReadFromJsonAsync<DataGridResponse<GetAllSupportingDocumentsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal(SupportingDocumentForView.SopHas, row.ForView);
        Assert.False(row.IsMandatory);

        var detailResponse = await client.GetAsync($"{Route}/{created.Id}");
        var detail = await detailResponse.Content
            .ReadFromJsonAsync<SupportingDocumentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("sop-has.pdf", detail.TemplateFileName);

        var templateResponse = await client.GetAsync($"{Route}/{created.Id}/template");

        Assert.Equal(HttpStatusCode.OK, templateResponse.StatusCode);
        Assert.Equal("application/pdf", templateResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([37, 80, 68, 70], await templateResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Create_DuplicateSequence_ReturnsConflictWithTheManualMessage()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);
        await client.PostAsync(Route, Form(), CancellationToken.None);

        var response = await client.PostAsync(Route, Form(), CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(SequenceExistsMessage, body);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_ReturnsBadRequest()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            Route,
            Form(forView: null, documentType: string.Empty, documentSequence: null),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            Route,
            Form(fileName: "setup.exe"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_TemplateOnNonSopView_ReturnsBadRequest()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            Route,
            Form(forView: "RawMaterial", fileName: "raw-material.pdf"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplate_UnknownId_ReturnsNotFound()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/template");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplate_RowWithoutTemplate_ReturnsNotFound()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync(withTemplate: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{rowId}/template");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutFile_KeepsTheTemplate()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync(withTemplate: true);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsync(
            $"{Route}/{rowId}",
            Form(documentType: "Halal Procedure MY", fileName: null),
            CancellationToken.None);
        var updated = await response.Content.ReadFromJsonAsync<SupportingDocumentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Halal Procedure MY", updated.DocumentType);
        Assert.Equal("template.pdf", updated.TemplateFileName);
    }

    [Fact]
    public async Task Update_WithFile_ReplacesTheTemplate()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync(withTemplate: true);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsync(
            $"{Route}/{rowId}",
            Form(fileName: "second.pdf"),
            CancellationToken.None);
        var updated = await response.Content.ReadFromJsonAsync<SupportingDocumentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("second.pdf", updated.TemplateFileName);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsync(
            $"{Route}/{Guid.NewGuid()}",
            Form(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var rowId = await factory.SeedRowAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{rowId}");
        var detailResponse = await client.GetAsync($"{Route}/{rowId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new SupportingDocumentsApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
