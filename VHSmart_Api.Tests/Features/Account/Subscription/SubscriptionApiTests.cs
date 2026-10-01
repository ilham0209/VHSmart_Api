using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VHSmart_Api.Features.Account.Password;
using VHSmart_Api.Features.Account.Subscription;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Subscriptions;
using VHSmart_Api.Tests.Features.Auth;

namespace VHSmart_Api.Tests.Features.Account.Subscription;

// End-to-end through the real pipeline: JWT -> JwtCurrentUser -> SubscriptionExpiryMiddleware
// -> HasPermission -> MediatR. The middleware is what makes D-11 observable (403 with the
// SUBSCRIPTION_EXPIRED code) and what keeps the subscription tab reachable while expired.
public class SubscriptionApiTests
{
    private const string Password = "Passw0rd!";

    private const string NewPassword = "N3wPassw0rd!";

    private const string SubscriptionRoute = "/api/account/subscription";

    private const string HistoryRoute = "/api/account/subscription/history";

    private const string ProfileRoute = "/api/account/profile";

    private const string PasswordRoute = "/api/account/password";

    [Fact]
    public async Task Subscription_WithoutToken_Returns401()
    {
        using var factory = new AuthApiFactory();
        await factory.SeedUserAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(SubscriptionRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Subscription_WithToken_ReturnsPackageExpiryAndRenewal()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        await AddSubscriptionAsync(factory, companyId, endDate: new DateTime(2033, 4, 22));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.GetAsync(SubscriptionRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscription = await response.Content.ReadFromJsonAsync<GetSubscriptionResponse>();
        Assert.Equal("ADC", subscription!.PackageCode);
        Assert.Equal("Advanced", subscription.PackageName);
        Assert.Equal(new DateTime(2033, 4, 21), subscription.ExpiryDate);
        Assert.Equal(new DateTime(2033, 4, 22), subscription.NextRenewalDate);
        Assert.False(subscription.IsOngoing);
    }

    [Fact]
    public async Task Subscription_WithoutAccountSettingPermission_Returns403WithoutExpiryCode()
    {
        using var factory = new AuthApiFactory();
        var role = await factory.SeedRoleWithoutPermissionsAsync();
        var user = await factory.SeedUserAsync(roleId: role.Id, isPlatformAdmin: false);
        using var client = factory.CreateClient();
        Authorize(client, factory, user, Guid.NewGuid());

        var response = await client.GetAsync(SubscriptionRoute);

        // The permission challenge (403 with an empty body), not the subscription problem:
        // this company has no rows, so the expiry middleware never fires. The D-11 response
        // is asserted by ExpiredSubscription_OtherEndpoint_Returns403WithExpiryCode.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task History_WithToken_ReturnsPagedRows()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        await AddSubscriptionAsync(factory, companyId, endDate: new DateTime(2033, 4, 22));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.GetAsync(HistoryRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<DataGridResponseEnvelope>();
        Assert.NotNull(history);
        Assert.Equal(1, history.TotalRecords);
        var row = Assert.Single(history.Data!);
        Assert.Equal(1, row.No);
        Assert.Equal("Advanced", row.PackageName);
        Assert.Equal(new DateTime(2033, 4, 22), row.EndDate);
    }

    [Fact]
    public async Task ExpiredSubscription_OtherEndpoint_Returns403WithExpiryCode()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        // Blocked from the End Date on (D-11): End Date = today.
        await AddSubscriptionAsync(factory, companyId, endDate: DateTime.UtcNow.Date);
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("SUBSCRIPTION_EXPIRED", problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ExpiringTomorrow_OtherEndpoint_Returns200()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        // End Date tomorrow = the display expiry (End Date - 1) is today: still valid.
        await AddSubscriptionAsync(factory, companyId, endDate: DateTime.UtcNow.Date.AddDays(1));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredSubscription_SubscriptionEndpoints_StayReachable()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        await AddSubscriptionAsync(factory, companyId, endDate: DateTime.UtcNow.Date.AddDays(-3));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var header = await client.GetAsync(SubscriptionRoute);
        var history = await client.GetAsync(HistoryRoute);

        // D-11: the tab that shows the expiry dates must not be behind the expiry block.
        Assert.Equal(HttpStatusCode.OK, header.StatusCode);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
    }

    [Fact]
    public async Task ExpiredSubscription_ChangePassword_StillWorks()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        await AddSubscriptionAsync(factory, companyId, endDate: DateTime.UtcNow.Date.AddDays(-3));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.PutAsJsonAsync(
            PasswordRoute, new ChangePasswordCommand(Password, NewPassword, NewPassword));

        // D-11: change password stays reachable so an expired company is never locked in.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoSubscriptionRows_NeverBlocked()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        using var client = factory.CreateClient();
        Authorize(client, factory, user, Guid.NewGuid());

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OngoingSubscription_NeverBlocked()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        var companyId = Guid.NewGuid();
        await AddSubscriptionAsync(factory, companyId, endDate: new DateTime(9999, 12, 31));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, companyId);

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredSubscription_OfAnotherCompany_DoesNotBlockThisOne()
    {
        using var factory = new AuthApiFactory();
        var user = await factory.SeedUserAsync();
        await AddSubscriptionAsync(factory, Guid.NewGuid(), endDate: DateTime.UtcNow.Date.AddDays(-3));
        using var client = factory.CreateClient();
        Authorize(client, factory, user, Guid.NewGuid());

        var response = await client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Program_RegistersSubscriptionWarningHostedService()
    {
        using var factory = new AuthApiFactory();

        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is SubscriptionExpiryWarningService);
    }

    private sealed record HistoryRow(
        Guid Id,
        int No,
        string? Label,
        string PackageName,
        int DurationMonths,
        DateTime StartDate,
        DateTime EndDate);

    private sealed class DataGridResponseEnvelope
    {
        public List<HistoryRow>? Data { get; set; }

        public int TotalRecords { get; set; }
    }

    private static async Task AddSubscriptionAsync(
        AuthApiFactory factory,
        Guid companyId,
        DateTime endDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        db.CompanySubscriptions.Add(new CompanySubscriptionEntity
        {
            CompanyId = companyId,
            PackageId = package.Id,
            EntryType = SubscriptionEntryType.New,
            DurationMonths = 12,
            StartDate = endDate.AddYears(-1).AddDays(1),
            EndDate = endDate
        });
        await db.SaveChangesAsync();
    }

    private static void Authorize(
        HttpClient client,
        AuthApiFactory factory,
        UserEntity user,
        Guid companyId)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateTokenFor(user, companyId));
    }
}
