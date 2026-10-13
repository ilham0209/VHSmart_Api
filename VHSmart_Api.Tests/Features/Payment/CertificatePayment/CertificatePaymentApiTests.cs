using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Payment.CertificatePayment;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

public class CertificatePaymentApiTests
{
    private const string Route = "/api/payment/certificate";

    private static readonly Guid PaymentRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        CertificatePaymentApiFactory factory,
        Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", factory.CreateToken(PaymentRoleId, userId));
        return client;
    }

    private static object CreateBody(Guid applicationId, Guid staffId, decimal amount = 500m) => new
    {
        halalApplicationId = applicationId,
        dateHalalApplication = "2026-10-01",
        chargeLetterDate = "2026-10-05",
        amount,
        referenceNo = "REF-1",
        bankName = "Bank Islam",
        bankAccountNo = "1234-5678",
        submittedByStaffId = staffId,
        submittedDate = "2026-10-06",
        receivedByStaffId = staffId,
        receivedDate = "2026-10-07",
        paymentVoucherNo = "PV-1"
    };

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new { }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsRowWithReadThroughColumns()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllCertificatePaymentsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(paymentId, row.Id);
        Assert.Equal("Seri Rasa", row.Brand);
        Assert.Equal("CB-001", row.CbApplicationNo);
        Assert.Equal("Halal Food", row.Scheme);
        Assert.Equal("APPLICATION APPROVED", row.ApplicationStatus);
        Assert.Equal(PaymentStatus.RequestBankDraft, row.PaymentStatus);
        Assert.Equal(500m, row.Amount);
    }

    [Fact]
    public async Task GetById_ReturnsDetail()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{paymentId}");
        var detail = await response.Content.ReadFromJsonAsync<CertificatePaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(paymentId, detail.Id);
        Assert.Equal("Seri Rasa", detail.Brand);
        Assert.Equal("Staff One", detail.SubmittedBy);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", detail.CertificationBody);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsDetailWithInitialStatus()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (schemeId, applicationId, staffId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, CreateBody(applicationId, staffId, amount: 750m), Json);
        var detail = await response.Content.ReadFromJsonAsync<CertificatePaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(750m, detail.Amount);
        Assert.Equal($"HAL({schemeId:N})/12102026/1", detail.ApplicationReferenceNo);
        Assert.Equal("PV-1", detail.PaymentVoucherNo);
    }

    [Fact]
    public async Task Create_InvalidAmount_ReturnsBadRequest()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, applicationId, staffId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, CreateBody(applicationId, staffId, amount: 0m), Json);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors, pair =>
            pair.Key == "Amount"
            && pair.Value.Contains("Amount must be greater than 0."));
    }

    [Fact]
    public async Task Update_AdvancesOneStep_ReturnsUpdatedStatus()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, staffId, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{paymentId}",
            new
            {
                dateHalalApplication = "2026-10-01",
                chargeLetterDate = "2026-10-05",
                amount = 500m,
                referenceNo = "REF-1",
                bankName = "Bank Islam",
                bankAccountNo = "1234-5678",
                submittedByStaffId = staffId,
                submittedDate = "2026-10-06",
                receivedByStaffId = staffId,
                receivedDate = "2026-10-07",
                paymentVoucherNo = "PV-1",
                status = PaymentStatus.ReceivedBankDraft
            },
            Json);
        var detail = await response.Content.ReadFromJsonAsync<CertificatePaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(PaymentStatus.ReceivedBankDraft, detail.Status);
    }

    [Fact]
    public async Task Update_SkippingStep_ReturnsConflict()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, staffId, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{paymentId}",
            new
            {
                amount = 500m,
                submittedByStaffId = staffId,
                submittedDate = "2026-10-06",
                receivedByStaffId = staffId,
                receivedDate = "2026-10-07",
                status = PaymentStatus.PaymentToCb
            },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{paymentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_Multipart_ReturnsRow()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent([37, 80, 68, 70]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", "bank-draft.pdf");
        content.Add(new StringContent("Bank Draft"), "DocumentType");

        var response = await client.PostAsync(
            $"{Route}/{paymentId}/attachments", content);
        var row = await response.Content.ReadFromJsonAsync<CertificatePaymentAttachmentResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(row);
        Assert.Equal("Bank Draft", row.DocumentType);
        Assert.Equal("bank-draft.pdf", row.FileName);
    }

    [Fact]
    public async Task UploadAttachment_BadExtension_ReturnsUnprocessableEntity()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "File", "payload.exe");
        content.Add(new StringContent("Bank Draft"), "DocumentType");

        var response = await client.PostAsync(
            $"{Route}/{paymentId}/attachments", content);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
    }

    [Fact]
    public async Task GetAttachments_ReturnsSeededRow()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{paymentId}/attachments");
        var list = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<CertificatePaymentAttachmentResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(list);
        var row = Assert.Single(list);
        Assert.Equal(attachmentId, row.Id);
        Assert.Equal("Charge Letter", row.DocumentType);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsFile()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"{Route}/{paymentId}/attachments/{attachmentId}/document");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ApplicationLookup_ReturnsApplicationRow()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, applicationId, _, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/application-lookup");
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<CertificatePaymentApplicationResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var row = Assert.Single(grid.Data);
        Assert.Equal(applicationId, row.Id);
        Assert.Equal("Seri Rasa", row.Brand);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", row.CertificationBody);
    }

    [Fact]
    public async Task StaffLookup_ReturnsStaffOptions()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, staffId, _, _) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/staff-lookup");
        var options = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<PaymentStaffOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        var row = Assert.Single(options);
        Assert.Equal(staffId, row.Id);
        Assert.Equal("Staff One", row.Name);
    }

    [Fact]
    public async Task DeleteAttachment_ReturnsNoContent()
    {
        using var factory = new CertificatePaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId, attachmentId) = await factory.SeedFullStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync(
            $"{Route}/{paymentId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
