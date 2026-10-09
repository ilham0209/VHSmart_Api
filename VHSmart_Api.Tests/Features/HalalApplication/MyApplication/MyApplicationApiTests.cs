using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

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

    [Fact]
    public async Task GetById_WithPermission_ReturnsTheScreen()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync("Sereni Trading Sdn Bhd");
        var applicationId = await factory.SeedApplicationAsync("VHS(PR)/01012026/7");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{applicationId}");
        var payload = await ReadAsync<ApplicationDetailResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(applicationId, payload.Id);
        Assert.Equal("VHS(PR)/01012026/7", payload.ReferenceNo);
        Assert.Equal("Sereni Trading Sdn Bhd", payload.CompanyName);
        Assert.Equal("DRAFT", payload.Status);
        Assert.True(payload.Survey.ReadProcedureManual);
        Assert.NotNull(payload.Company);
        Assert.NotNull(payload.Extras);
        Assert.Empty(payload.AdditionalInformation);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Application not found.", problem.Detail);
    }

    [Fact]
    public async Task Update_ViewOnlyRole_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: [PermissionAction.View]);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}",
            new UpdateApplicationCommand(
                Guid.Empty, "Renewal", null, null, null, null),
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidCommand_ReturnsTheHeader()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}",
            new UpdateApplicationCommand(
                Guid.Empty,
                "renewal",
                "CB-2026-001",
                new DateOnly(2026, 10, 1),
                "Coach Rahim",
                null),
            Json);
        var payload = await ReadAsync<ApplicationHeaderResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(applicationId, payload.Id);
        // The route id wins over whatever the body carried (the same stance as UpdateBatch).
        Assert.Equal("Renewal", payload.ApplicationType);
        Assert.Equal("CB-2026-001", payload.CbApplicationNo);
        Assert.Equal("Coach Rahim", payload.HalalCoachName);
        Assert.Null(payload.BatchName);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}",
            new UpdateApplicationCommand(
                Guid.Empty, "New", null, null, null, null),
            Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Application not found.", problem?.Detail);
    }

    [Fact]
    public async Task UpdateCompanyInformation_ValidCommand_ReturnsTheExtras()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/company-information",
            new UpdateCompanyInformationCommand(
                Guid.Empty,
                YearlySalesRevenue: "2500000",
                ProductMarket: "Domestic",
                WorkingHourFrom: new TimeOnly(8, 0),
                WorkingHourTo: new TimeOnly(17, 0),
                NumberOfShifts: 2,
                MuslimManagement: 4,
                MuslimFoodHandler: 10,
                MuslimChef: 3,
                NonMuslimManagement: 1,
                NonMuslimFoodHandler: 6,
                NonMuslimChef: 2),
            Json);
        var payload = await ReadAsync<ApplicationCompanyExtrasResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("2500000", payload.YearlySalesRevenue);
        Assert.Equal("Domestic", payload.ProductMarket);
        Assert.Equal(2, payload.NumberOfShifts);
    }

    [Fact]
    public async Task UpdateCompanyInformation_NegativeCounter_Returns400()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/company-information",
            new UpdateCompanyInformationCommand(
                Guid.Empty, null, null, null, null,
                NumberOfShifts: -1,
                null, null, null, null, null, null),
            Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            "Employee counts and number of shifts must be 0 or more.",
            problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task UpdateAdditionalInformation_ValidCommand_ReturnsTheItems()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/additional-information",
            new UpdateAdditionalInformationCommand(
                applicationId,
                [
                    new AdditionalInfoItemInput(
                        ApplicationAdditionalInfoSection.Packaging, "CARTON_BOX", null),
                    new AdditionalInfoItemInput(
                        ApplicationAdditionalInfoSection.QualityControl, "OTHERS",
                        "Third party audit")
                ]),
            Json);
        var payload = await ReadAsync<ApplicationAdditionalInfoItemResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(2, payload.Length);
        Assert.Equal("CARTON_BOX", payload[0].OptionCode);
        Assert.Equal("OTHERS", payload[1].OptionCode);
        Assert.Equal("Third party audit", payload[1].FreeText);
        // Enum round-trips as its string name (JsonStringEnumConverter).
        Assert.Equal("Packaging", payload[0].Section.ToString());
    }

    [Fact]
    public async Task UpdateAdditionalInformation_UnknownOption_Returns400()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/additional-information",
            new UpdateAdditionalInformationCommand(
                applicationId,
                [
                    new AdditionalInfoItemInput(
                        ApplicationAdditionalInfoSection.Packaging, "WOODEN_CRATE", null)
                ]),
            Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            "Additional information option is not valid for its section.",
            problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task GetEstablishments_WithoutBatch_ReturnsAnEmptyArray()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{applicationId}/establishments");
        var payload = await ReadAsync<ApplicationEstablishmentResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public async Task GetProducts_WithoutBatch_ReturnsAnEmptyArray()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{applicationId}/products");
        var payload = await ReadAsync<ApplicationProductResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public async Task GetRawMaterials_WithoutBatch_ReturnsAnEmptyArray()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{applicationId}/raw-materials");
        var payload = await ReadAsync<ApplicationRawMaterialResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public async Task GetById_ForeignApplicationToken_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory, companyId: Guid.NewGuid());

        var response = await client.GetAsync($"{Route}/{applicationId}");
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Application not found.", problem?.Detail);
    }

    private static MultipartFormDataContent AttachmentForm(
        Guid documentTypeId,
        string? fileName = "supporting.pdf")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(documentTypeId.ToString()), "DocumentTypeId");
        if (fileName is not null)
        {
            var file = new ByteArrayContent([37, 80, 68, 70]); // %PDF magic bytes
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "File", fileName);
        }

        return content;
    }

    private static SubmitApplicationCommand Acknowledgement(Guid applicationId) =>
        new(
            applicationId,
            "Ahmad bin Ali",
            "ahmad@example.com",
            "+60123456789",
            true);

    [Fact]
    public async Task UploadAttachments_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: [PermissionAction.View]);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{applicationId}/attachments", AttachmentForm(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachments_ValidForm_ReturnsTheUploadedRow()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        var documentTypeId = await factory.SeedAsync(db =>
            ApplicationTestData.SeedSupportingDocumentAsync(db, "HAS", companyId: factory.CompanyId));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{applicationId}/attachments", AttachmentForm(documentTypeId, "upload.pdf"));
        var payload = await ReadAsync<ApplicationAttachmentResponse[]>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        var row = Assert.Single(payload);
        Assert.Equal("HAS", row.DocumentType);
        Assert.Equal("upload.pdf", row.FileName);

        // The list and the streamed download (the Action cell) answer the same row.
        var list = await client.GetAsync($"{Route}/{applicationId}/attachments");
        var listed = Assert.Single(
            await ReadAsync<ApplicationAttachmentResponse[]>(list) ?? []);
        Assert.Equal(documentTypeId, listed.DocumentTypeId);

        var download = await client.GetAsync(
            $"{Route}/{applicationId}/attachments/{row.Id}/document");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(
            new byte[] { 37, 80, 68, 70 },
            await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadAttachments_DisallowedFileType_ReturnsUnprocessableEntity()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        var documentTypeId = await factory.SeedAsync(db =>
            ApplicationTestData.SeedSupportingDocumentAsync(db, "HAS", companyId: factory.CompanyId));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync(
            $"{Route}/{applicationId}/attachments", AttachmentForm(documentTypeId, "payload.exe"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: [PermissionAction.View]);
        await factory.SeedDatabaseAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{applicationId}/submit", Acknowledgement(applicationId), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_NoBatch_Returns422WithTheBatchMessage()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{applicationId}/submit", Acknowledgement(applicationId), Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("A batch must be selected before submission.", problem.Detail);
    }

    [Fact]
    public async Task Submit_CheckboxUnchecked_Returns400WithTheAcknowledgementMessage()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await factory.SeedApplicationAsync();
        using var client = CreateAuthorizedClient(factory);

        var command = Acknowledgement(applicationId) with { AckAccepted = false };
        var response = await client.PostAsJsonAsync(
            $"{Route}/{applicationId}/submit", command, Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            "The acknowledgement must be accepted.",
            problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task Submit_FullyReadyApplication_ReturnsProcessingAtTheCb()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        await factory.SeedAsync(db =>
            ApplicationTestData.SeedCertificationBodyAsync(db, factory.CompanyId));
        var batchId = await factory.SeedAsync(async db =>
        {
            var schemeId = await BatchTestData.FoodPremiseSchemeIdAsync(db);
            return await BatchTestData.SeedBatchAsync(
                db, factory.CompanyId, schemeId: schemeId);
        });
        var premiseId = await factory.SeedAsync(db =>
            BatchTestData.SeedCompletePremiseAsync(db, factory.CompanyId));
        await factory.SeedAsync(db =>
            BatchTestData.SeedBatchPremiseAsync(db, factory.CompanyId, batchId, premiseId));
        var applicationId = await factory.SeedAsync(db =>
            ApplicationTestData.SeedApplicationAsync(
                db,
                factory.CompanyId,
                batchId: batchId,
                cbApplicationNo: "CB-2026-001",
                cbApplicationDate: new DateOnly(2026, 10, 1)));
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"{Route}/{applicationId}/submit", Acknowledgement(applicationId), Json);
        var payload = await ReadAsync<ApplicationSubmitResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("PROCESSING AT JAKIM (NEW)", payload.Status);
        Assert.NotNull(payload.SubmittedAt);

        // The header block of the screen now shows the submit state.
        var detail = await ReadAsync<ApplicationDetailResponse>(
            await client.GetAsync($"{Route}/{applicationId}"));
        Assert.NotNull(detail);
        Assert.Equal("PROCESSING AT JAKIM (NEW)", detail.Status);
    }

    private static Task<Guid> SeedSubmittedApplicationAsync(
        MyApplicationApiFactory factory,
        string status = "PROCESSING AT JAKIM (NEW)") =>
        factory.SeedAsync(db => ApplicationTestData.SeedApplicationAsync(
            db, factory.CompanyId, status: status));

    [Fact]
    public async Task TagStatus_WithoutEditPermission_ReturnsForbidden()
    {
        using var factory = new MyApplicationApiFactory(
            AdminRoleId, grantedActions: [PermissionAction.View]);
        await factory.SeedDatabaseAsync();
        var applicationId = await SeedSubmittedApplicationAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/status",
            new TagApplicationStatusCommand(
                applicationId, ApplicationStatus.ApplicationApproved, null),
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TagStatus_FromProcessing_ReturnsTheApprovedStatus()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await SeedSubmittedApplicationAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/status",
            new TagApplicationStatusCommand(
                applicationId, ApplicationStatus.ApplicationApproved, "Audit booked."),
            Json);
        var payload = await ReadAsync<ApplicationStatusTagResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(ApplicationStatus.ApplicationApproved, payload.Status);

        var detail = await ReadAsync<ApplicationDetailResponse>(
            await client.GetAsync($"{Route}/{applicationId}"));
        Assert.NotNull(detail);
        Assert.Equal(ApplicationStatus.ApplicationApproved, detail.Status);
    }

    [Fact]
    public async Task TagStatus_Backwards_ReturnsUnprocessableEntity()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        await factory.SeedCompanyAsync();
        var applicationId = await SeedSubmittedApplicationAsync(
            factory, ApplicationStatus.AuditInProgress);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/status",
            new TagApplicationStatusCommand(
                applicationId, ApplicationStatus.ApplicationApproved, null),
            Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Application status can only move forward.", problem.Detail);
    }

    [Fact]
    public async Task TagStatus_UnknownStatusValue_Returns400WithTheMessage()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var applicationId = await SeedSubmittedApplicationAsync(factory);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/status",
            new TagApplicationStatusCommand(applicationId, "Payment Confirmed", null),
            Json);
        var problem = await ReadAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            "Status must be Application Approved, Audit (In Progress), "
                + "Audit (Completed) or Approved with Document.",
            problem.Errors.Values.SelectMany(value => value));
    }

    [Fact]
    public async Task TagStatus_ForeignApplicationToken_ReturnsNotFound()
    {
        using var factory = new MyApplicationApiFactory(AdminRoleId);
        await factory.SeedDatabaseAsync();
        var applicationId = await SeedSubmittedApplicationAsync(factory);
        using var client = CreateAuthorizedClient(factory, companyId: Guid.NewGuid());

        var response = await client.PutAsJsonAsync(
            $"{Route}/{applicationId}/status",
            new TagApplicationStatusCommand(
                applicationId, ApplicationStatus.ApplicationApproved, null),
            Json);
        var problem = await ReadAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Application not found.", problem?.Detail);
    }
}
