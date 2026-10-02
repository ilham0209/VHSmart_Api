using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class StaffApiTests
{
    private const string Route = "/api/personnel/staff";

    private static readonly Guid AdminRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(StaffApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken(AdminRoleId));
        return client;
    }

    // The Manage Staff form as JSON (spec 7.4): no company and no photo in the body - the
    // company comes from the JWT and the photo is its own endpoint. Null ids leave the field
    // out (System.Text.Json handles explicit nulls anyway).
    private static object StaffBody(
        Guid titleId,
        Guid departmentId,
        Guid designationId,
        string email = "staff@verify.my",
        Guid? id = null) => new
        {
            Id = id ?? Guid.NewGuid(),
            Email = email,
            TitleId = titleId,
            Name = "Siti Aminah",
            IdType = "NRIC",
            IdNumber = "900101011234",
            EmployeeIdNumber = "EMP-01",
            Gender = "Female",
            Religion = "Islam",
            DepartmentId = departmentId,
            DesignationId = designationId,
            OfficeNumber = "0312345678",
            MobileNumber = "0123456789",
            HasTyphoidInjection = false,
            TyphoidExpiryDate = (string?)null,
            IsIhcMember = false,
            IhcRoleId = (Guid?)null
        };

    private static MultipartFormDataContent PhotoForm(string fileName = "photo.png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([137, 80, 78, 71]); // PNG magic bytes
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "File", fileName);
        return content;
    }

    private static MultipartFormDataContent AttachmentForm(Guid documentTypeId, string fileName = "report.pdf")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(documentTypeId.ToString()), "DocumentTypeId");
        var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", fileName);
        return content;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new StaffApiFactory(AdminRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new StaffApiFactory(AdminRoleId, grantPermissions: false);
        await factory.SeedDatabaseAsync();
        var people = await factory.SeedPeopleDataAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, StaffBody(people.TitleId, people.DepartmentId, people.DesignationId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedEmptyCompany_ReturnsEmptyGrid()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllStaffResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task GetOptions_ReturnsTheFourPeopleLists()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedPeopleDataAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/options");
        var options = await response.Content.ReadFromJsonAsync<GetStaffOptionsResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        Assert.Equal("Mr", Assert.Single(options.Titles).Name);
        Assert.Equal("Production", Assert.Single(options.Departments).Name);
        Assert.Equal("Halal Executive", Assert.Single(options.Designations).Name);
        Assert.Equal("Member", Assert.Single(options.IhcRoles).Name);
    }

    [Fact]
    public async Task Create_ValidForm_ThenGetByIdAndListShowIt()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var people = await factory.SeedPeopleDataAsync();
        using var client = CreateAuthorizedClient(factory);

        var createResponse = await client.PostAsJsonAsync(
            Route, StaffBody(people.TitleId, people.DepartmentId, people.DesignationId));
        var created = await createResponse.Content.ReadFromJsonAsync<StaffResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Siti Aminah", created.Name);
        Assert.False(string.IsNullOrWhiteSpace(created.CompanyName));

        var detailResponse = await client.GetAsync($"{Route}/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<StaffResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("staff@verify.my", detail.Email);

        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllStaffResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal(1, row.No);
        Assert.Equal("Halal Executive", row.Designation);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var people = await factory.SeedPeopleDataAsync();
        using var client = CreateAuthorizedClient(factory);
        await client.PostAsJsonAsync(Route, StaffBody(people.TitleId, people.DepartmentId, people.DesignationId));

        var response = await client.PostAsJsonAsync(
            Route, StaffBody(people.TitleId, people.DepartmentId, people.DesignationId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_ReturnsBadRequest()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new
        {
            Email = string.Empty,
            TitleId = Guid.Empty,
            Name = string.Empty,
            DepartmentId = Guid.Empty,
            DesignationId = Guid.Empty,
            HasTyphoidInjection = false,
            IsIhcMember = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingRow_ReturnsOkWithNewName()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var people = await factory.SeedPeopleDataAsync();
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        var body = StaffBody(
            people.TitleId, people.DepartmentId, people.DesignationId, id: staffId);
        var response = await client.PutAsJsonAsync($"{Route}/{staffId}", body);
        var updated = await response.Content.ReadFromJsonAsync<StaffResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Siti Aminah", updated.Name);
        Assert.Equal(staffId, updated.Id);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var people = await factory.SeedPeopleDataAsync();
        using var client = CreateAuthorizedClient(factory);
        var unknownId = Guid.NewGuid();

        var response = await client.PutAsJsonAsync(
            $"{Route}/{unknownId}",
            StaffBody(people.TitleId, people.DepartmentId, people.DesignationId, id: unknownId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingRow_ReturnsNoContentAndRowDisappears()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{staffId}");
        var listResponse = await client.GetAsync(Route);
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllStaffResponse>>(Json);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ThenGetPhoto_ReturnsOkAndStreamsTheBytes()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        var uploadResponse = await client.PutAsync(
            $"{Route}/{staffId}/photo", PhotoForm(), CancellationToken.None);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UploadStaffPhotoResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal("photo.png", uploaded.FileName);

        var getResponse = await client.GetAsync($"{Route}/{staffId}/photo");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([137, 80, 78, 71], await getResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadPhoto_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsync(
            $"{Route}/{staffId}/photo", PhotoForm("policy.pdf"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_ThenListDocumentAndDelete()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var staffId = await factory.SeedStaffAsync();
        var documentTypeId = await factory.SeedDocumentTypeAsync();
        using var client = CreateAuthorizedClient(factory);

        var uploadResponse = await client.PostAsync(
            $"{Route}/{staffId}/attachments", AttachmentForm(documentTypeId), CancellationToken.None);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<StaffAttachmentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal("Medical Report", uploaded.DocumentType);

        var listResponse = await client.GetAsync($"{Route}/{staffId}/attachments");
        var grid = await listResponse.Content.ReadFromJsonAsync<DataGridResponse<GetAllStaffAttachmentsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal("report.pdf", Assert.Single(grid.Data).FileName);

        var documentResponse = await client.GetAsync(
            $"{Route}/attachments/{uploaded.Id}/document");

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/pdf", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([37, 80, 68, 70], await documentResponse.Content.ReadAsByteArrayAsync());

        var deleteResponse = await client.DeleteAsync($"{Route}/attachments/{uploaded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var afterDelete = await client.GetAsync($"{Route}/{staffId}/attachments");
        var emptied = await afterDelete.Content.ReadFromJsonAsync<DataGridResponse<GetAllStaffAttachmentsResponse>>(Json);
        Assert.NotNull(emptied);
        Assert.Equal(0, emptied.TotalRecords);
    }

    [Fact]
    public async Task UploadAttachment_DocumentTypeNotForAllStaff_ReturnsBadRequest()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var staffId = await factory.SeedStaffAsync();
        var trainingType = await factory.SeedDocumentTypeAsync(
            "Training Register", SupportingDocumentForView.Training);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{staffId}/attachments", AttachmentForm(trainingType), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not for All Staff", body);
    }

    [Fact]
    public async Task GetAttachmentDocument_UnknownId_ReturnsNotFound()
    {
        using var factory = new StaffApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/attachments/{Guid.NewGuid()}/document");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
