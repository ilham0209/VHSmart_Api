using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class MinutesMeetingApiTests
{
    private const string Route = "/api/company-information/internal-halal-committee";

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return options;
    }

    private static HttpClient CreateAuthorizedClient(IhcApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }

    // The "Meeting Details" form as JSON (spec 7.6): company is read-only display and comes
    // from the JWT, so it is never part of the body.
    private static object MeetingBody(
        string title = "IHC Monthly Meeting",
        string meetingDate = "2026-04-15",
        string startTime = "09:00:00",
        string endTime = "11:00:00",
        string location = "HQ Meeting Room") => new
        {
            title,
            meetingDate,
            startTime,
            endTime,
            location
        };

    private static MultipartFormDataContent AttachmentForm(string? fileName = "minutes.pdf")
    {
        var content = new MultipartFormDataContent();
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
        using var factory = new IhcApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new IhcApiFactory(grantView: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new IhcApiFactory(grantCreate: false);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, MeetingBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new IhcApiFactory(grantEdit: false);
        await factory.SeedDatabaseAsync();
        var meetingId = await factory.SeedMeetingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync($"{Route}/{meetingId}", MeetingBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new IhcApiFactory(grantDelete: false);
        await factory.SeedDatabaseAsync();
        var meetingId = await factory.SeedMeetingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{meetingId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ShowsOwnMeetingWithCompanyCell()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        await factory.SeedMeetingAsync("Older meeting", date: new DateTime(2026, 1, 10));
        await factory.SeedMeetingAsync("Newer meeting", date: new DateTime(2026, 6, 20));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllMinutesMeetingsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Newer meeting", "Older meeting" },
            rows.Select(row => row.Title));
        Assert.Equal("Verify Halal Sdn Bhd", rows[0].Company);
    }

    [Fact]
    public async Task Chart_ReturnsIhcMembersWithRole()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        await factory.SeedStaffAsync("Siti Aminah", ihcMember: true);
        await factory.SeedStaffAsync("Plain Staff", ihcMember: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/organisation-chart");
        var rows = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<OrganisationChartResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rows);
        var member = Assert.Single(rows);
        Assert.Equal("Siti Aminah", member.Name);
        Assert.Equal("Member", member.Role);
        Assert.Equal(1, member.No);
        Assert.Equal("Verify Halal Sdn Bhd", member.Company);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsTheSavedState()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        using var client = CreateAuthorizedClient(factory);

        var createResponse = await client.PostAsJsonAsync(Route, MeetingBody(
            title: "Audit Review",
            meetingDate: "2026-05-05",
            startTime: "14:30:00",
            endTime: "16:00:00",
            location: "HQ Level 3"));
        var created = await createResponse.Content
            .ReadFromJsonAsync<MinutesMeetingDetailResponse>(Json);
        var getResponse = await client.GetAsync($"{Route}/{created?.Id}");
        var loaded = await getResponse.Content
            .ReadFromJsonAsync<MinutesMeetingDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Audit Review", created.Title);
        Assert.Equal("Verify Halal Sdn Bhd", created.Company);
        Assert.Equal(new TimeOnly(14, 30), created.StartTime);
        Assert.Equal(new TimeOnly(16, 0), created.EndTime);
        Assert.Empty(created.Attachments);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal("HQ Level 3", loaded.Location);
    }

    [Fact]
    public async Task Create_MissingTitle_ReturnsBadRequest()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, MeetingBody(title: " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EndBeforeStart_ReturnsBadRequest()
    {
        // D-14 (the spec 7.6 data quirk 18:32 to 15:35).
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, MeetingBody(startTime: "18:32:00", endTime: "15:35:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownOrForeignMeeting_ReturnsNotFound()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignMeeting = await factory.SeedMeetingAsync(
            "Foreign meeting", companyId: foreignCompany);
        using var client = CreateAuthorizedClient(factory);

        var unknown = await client.GetAsync($"{Route}/{Guid.NewGuid()}");
        var foreign = await client.GetAsync($"{Route}/{foreignMeeting}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Update_ForeignMeeting_ReturnsNotFoundAndChangesNothing()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        var foreignCompany = await factory.SeedForeignCompanyAsync();
        var foreignMeeting = await factory.SeedMeetingAsync(
            "Foreign meeting", companyId: foreignCompany);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{foreignMeeting}", MeetingBody(title: "Renamed"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Foreign meeting", await factory.MeetingTitleAsync(foreignMeeting));
    }

    [Fact]
    public async Task Delete_ThenGetById_ReturnsNotFound()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        var meetingId = await factory.SeedMeetingAsync();
        using var client = CreateAuthorizedClient(factory);

        var deleteResponse = await client.DeleteAsync($"{Route}/{meetingId}");
        var getResponse = await client.GetAsync($"{Route}/{meetingId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Null(await factory.MeetingTitleAsync(meetingId));
    }

    [Fact]
    public async Task Upload_ThenDownloadThenDeleteAttachment()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        var meetingId = await factory.SeedMeetingAsync();
        using var client = CreateAuthorizedClient(factory);

        var uploadResponse = await client.PostAsync(
            $"{Route}/{meetingId}/attachments", AttachmentForm());
        var detail = await uploadResponse.Content
            .ReadFromJsonAsync<MinutesMeetingDetailResponse>(Json);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(detail);
        var attachment = Assert.Single(detail.Attachments);
        Assert.Equal("minutes.pdf", attachment.FileName);

        var downloadResponse = await client.GetAsync(
            $"{Route}/{meetingId}/attachments/{attachment.Id}/document");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("%PDF", await downloadResponse.Content.ReadAsStringAsync());

        var deleteResponse = await client.DeleteAsync(
            $"{Route}/{meetingId}/attachments/{attachment.Id}");
        var downloadAfterDelete = await client.GetAsync(
            $"{Route}/{meetingId}/attachments/{attachment.Id}/document");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, downloadAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Upload_UnknownMeeting_ReturnsNotFound()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{Guid.NewGuid()}/attachments", AttachmentForm());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new IhcApiFactory();
        await factory.SeedDatabaseAsync();
        var meetingId = await factory.SeedMeetingAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{meetingId}/attachments", AttachmentForm("payload.exe"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
