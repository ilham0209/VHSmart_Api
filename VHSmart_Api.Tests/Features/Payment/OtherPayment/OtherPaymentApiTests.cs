using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Payment.OtherPayment;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

public class OtherPaymentApiTests
{
    private const string Route = "/api/payment/other";

    private static readonly Guid PaymentRoleId = RoleSeedData.VhSmartAdminRoleId;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static HttpClient CreateAuthorizedClient(
        OtherPaymentApiFactory factory,
        Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", factory.CreateToken(PaymentRoleId, userId));
        return client;
    }

    private static object CreateBody(Guid categoryId, Guid payeeId, Guid staffId, decimal amount = 150m) => new
    {
        paymentCategoryId = categoryId,
        serviceProviderId = payeeId,
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
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCreatePermission_ReturnsForbidden()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(Route, new { }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsRowWithReadThroughColumns()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(Route);
        var grid = await response.Content.ReadFromJsonAsync<
            DataGridResponse<GetAllOtherPaymentsResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(paymentId, row.Id);
        Assert.Equal("CATEGORY A", row.PaymentCategory);
        Assert.Equal("SERUNAI COMMERCE", row.Payee);
        Assert.Equal("MAYBANK", row.PayeeBankName);
        Assert.Equal("1234-5678", row.PayeeBankAccountNo);
        Assert.Equal(PaymentStatus.RequestBankDraft, row.PaymentStatus);
        Assert.Equal(150m, row.Amount);
        Assert.Equal("ABC", row.PaymentVoucherNo);
    }

    [Fact]
    public async Task GetById_ReturnsDetail()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{paymentId}");
        var detail = await response.Content.ReadFromJsonAsync<OtherPaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(paymentId, detail.Id);
        Assert.Equal("CATEGORY A", detail.PaymentCategory);
        Assert.Equal("SERUNAI COMMERCE", detail.Payee);
        Assert.Equal("Staff One", detail.SubmittedBy);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsDetailWithInitialStatus()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (categoryId, payeeId, staffId, _) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, CreateBody(categoryId, payeeId, staffId, amount: 300m), Json);
        var detail = await response.Content.ReadFromJsonAsync<OtherPaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(PaymentStatus.RequestBankDraft, detail.Status);
        Assert.Equal(300m, detail.Amount);
        Assert.Equal("PV-1", detail.PaymentVoucherNo);
    }

    [Fact]
    public async Task Create_InvalidAmount_ReturnsBadRequest()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (categoryId, payeeId, staffId, _) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            Route, CreateBody(categoryId, payeeId, staffId, amount: 0m), Json);
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
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (categoryId, payeeId, staffId, paymentId) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{paymentId}",
            new
            {
                paymentCategoryId = categoryId,
                serviceProviderId = payeeId,
                amount = 150m,
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
        var detail = await response.Content.ReadFromJsonAsync<OtherPaymentDetailResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(PaymentStatus.ReceivedBankDraft, detail.Status);
    }

    [Fact]
    public async Task Update_SkippingStep_ReturnsConflict()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (categoryId, payeeId, staffId, paymentId) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PutAsJsonAsync(
            $"{Route}/{paymentId}",
            new
            {
                paymentCategoryId = categoryId,
                serviceProviderId = payeeId,
                amount = 150m,
                submittedByStaffId = staffId,
                submittedDate = "2026-10-06",
                receivedByStaffId = staffId,
                receivedDate = "2026-10-07",
                status = PaymentStatus.PaymentToServiceProvider
            },
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (_, _, _, paymentId) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{paymentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutDeletePermission_ReturnsForbidden()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId, grantPermissions: false);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync($"{Route}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PayeeLookup_ReturnsProviderOptions()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (_, payeeId, _, _) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/payee-lookup");
        var options = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<OtherPaymentPayeeOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        var row = Assert.Single(options);
        Assert.Equal(payeeId, row.Id);
        Assert.Equal("SERUNAI COMMERCE", row.Name);
    }

    [Fact]
    public async Task PaymentCategoryLookup_ReturnsCategoryOptions()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (categoryId, _, _, _) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/payment-category-lookup");
        var options = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<OtherPaymentCategoryOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        var row = Assert.Single(options);
        Assert.Equal(categoryId, row.Id);
        Assert.Equal("CATEGORY A", row.Name);
    }

    [Fact]
    public async Task StaffLookup_ReturnsStaffOptions()
    {
        using var factory = new OtherPaymentApiFactory(PaymentRoleId);
        var (_, _, staffId, _) = await factory.SeedStackAsync();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync($"{Route}/staff-lookup");
        var options = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<OtherPaymentStaffOptionResponse>>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(options);
        var row = Assert.Single(options);
        Assert.Equal(staffId, row.Id);
        Assert.Equal("Staff One", row.Name);
    }
}
