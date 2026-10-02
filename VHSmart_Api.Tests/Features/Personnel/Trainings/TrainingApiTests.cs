using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class TrainingApiTests
{
    private const string Route = "/api/personnel/trainings";

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(TrainingApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    // The Manage Training form as JSON (spec 7.5): company is read-only display and comes from
    // the JWT, so it is never part of the body.
    private static object TrainingBody(
        string name = "Halal Awareness 101",
        string? trainingType = "AllStaff",
        IReadOnlyList<Guid>? attendees = null,
        string trainingDate = "2026-03-15") => new
        {
            trainingType,
            name,
            trainingDate,
            attendeeStaffIds = attendees ?? []
        };

    private static MultipartFormDataContent ModuleForm(
        Guid? moduleTypeId,
        string moduleName = "Slide 1",
        string? fileName = "module.pdf")
    {
        var content = new MultipartFormDataContent();
        if (moduleTypeId.HasValue)
            content.Add(new StringContent(moduleTypeId.Value.ToString()), "ModuleTypeId");
        content.Add(new StringContent(moduleName), "ModuleName");
        if (fileName is not null)
        {
            var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "File", fileName);
        }

        return content;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new TrainingApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new TrainingApiFactory(grantView: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new TrainingApiFactory(grantCreate: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, TrainingBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new TrainingApiFactory(grantEdit: false);
        await factory.SeedDatabaseAsync();
        var trainingId = await factory.SeedTrainingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{trainingId}", TrainingBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteModule_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new TrainingApiFactory(grantDelete: false);
        await factory.SeedDatabaseAsync();
        var moduleTypeId = await factory.SeedModuleTypeAsync();
        var trainingId = await factory.SeedTrainingAsync();
        var moduleId = await factory.SeedModuleAsync(trainingId, moduleTypeId);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{trainingId}/modules/{moduleId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ShowsOwnTrainingWithCompanyCell()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        await factory.SeedTrainingAsync("Halal Awareness 101");
        await factory.SeedTrainingAsync("Slaughtering Course", new DateTime(2026, 9, 1));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllTrainingsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Slaughtering Course", "Halal Awareness 101" },
            rows.Select(row => row.Name));
        Assert.Equal("Verify Halal Sdn Bhd", rows[0].Company);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsTheSavedState()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var staffId = await factory.SeedStaffAsync("Siti Aminah", ihcMember: true);
        using var client = CreateAuthorizedClient(factory);

        var createResponse = await client.PostAsJsonAsync(
            Route,
            TrainingBody(
                name: "IHC Briefing",
                trainingType: "InternalHalalCommittee",
                attendees: [staffId]));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingDetailResponse>(Json);
        var getResponse = await client.GetAsync($"{Route}/{created?.Id}");
        var loaded = await getResponse.Content.ReadFromJsonAsync<TrainingDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("IHC Briefing", created.Name);
        Assert.Equal(TrainingType.InternalHalalCommittee, created.TrainingType);
        Assert.Equal("Verify Halal Sdn Bhd", created.Company);
        var attendee = Assert.Single(created.Attendees);
        Assert.Equal("Siti Aminah", attendee.Name);
        Assert.True(attendee.IsIhcMember);
        Assert.Equal("Member", attendee.IhcRole);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Empty(loaded.Modules);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedTrainingAsync("Halal Awareness 101");
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, TrainingBody(name: "Halal Awareness 101", attendees: [staffId]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingTrainingTypeOrAttendance_ReturnsBadRequest()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var noType = await client.PostAsJsonAsync(
            Route, TrainingBody(trainingType: null));
        var noAttendees = await client.PostAsJsonAsync(
            Route, TrainingBody(attendees: []));

        Assert.Equal(HttpStatusCode.BadRequest, noType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noAttendees.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownOrForeignTraining_ReturnsNotFound()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignTraining = await factory.SeedTrainingAsync(
            "Foreign course", companyId: foreignCompany);
        using var client = CreateAuthorizedClient(factory);

        var unknown = await client.GetAsync($"{Route}/{Guid.NewGuid()}");
        var foreign = await client.GetAsync($"{Route}/{foreignTraining}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Update_ForeignTraining_ReturnsNotFoundAndChangesNothing()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignTraining = await factory.SeedTrainingAsync(
            "Foreign course", companyId: foreignCompany);
        var staffId = await factory.SeedStaffAsync();
        using var client = CreateAuthorizedClient(factory);

        // Attendance must pass the validator before the handler can return its 404.
        var response = await client.PutAsJsonAsync(
            $"{Route}/{foreignTraining}", TrainingBody(name: "Renamed", attendees: [staffId]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Foreign course", await factory.TrainingNameAsync(foreignTraining));
    }

    [Fact]
    public async Task StaffOptions_ReturnsOwnStaff()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedStaffAsync("Siti Aminah");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/staff-options");
        var options = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<TrainingStaffOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        var option = Assert.Single(options);
        Assert.Equal("Siti Aminah", option.Name);
        Assert.Equal("Halal Executive", option.Designation);
    }

    [Fact]
    public async Task Module_UploadThenDownloadThenDelete()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var moduleTypeId = await factory.SeedModuleTypeAsync();
        var trainingId = await factory.SeedTrainingAsync();
        using var client = CreateAuthorizedClient(factory);

        var uploadResponse = await client.PostAsync(
            $"{Route}/{trainingId}/modules", ModuleForm(moduleTypeId));
        var detail = await uploadResponse.Content.ReadFromJsonAsync<TrainingDetailResponse>(Json);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(detail);
        var module = Assert.Single(detail.Modules);
        Assert.Equal("Slide 1", module.ModuleName);
        Assert.Equal("Presentation", module.ModuleType);
        Assert.Equal("module.pdf", module.FileName);

        var downloadResponse = await client.GetAsync(
            $"{Route}/{trainingId}/modules/{module.Id}/document");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("%PDF", await downloadResponse.Content.ReadAsStringAsync());

        var deleteResponse = await client.DeleteAsync(
            $"{Route}/{trainingId}/modules/{module.Id}");
        var downloadAfterDelete = await client.GetAsync(
            $"{Route}/{trainingId}/modules/{module.Id}/document");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, downloadAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Module_UnknownModuleType_ReturnsBadRequest()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var trainingId = await factory.SeedTrainingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{trainingId}/modules", ModuleForm(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Module_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var moduleTypeId = await factory.SeedModuleTypeAsync();
        var trainingId = await factory.SeedTrainingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{trainingId}/modules",
            ModuleForm(moduleTypeId, fileName: "payload.exe"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Module_UnknownTraining_ReturnsNotFound()
    {
        using var factory = new TrainingApiFactory();
        await factory.SeedDatabaseAsync();
        var moduleTypeId = await factory.SeedModuleTypeAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/modules", ModuleForm(moduleTypeId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
