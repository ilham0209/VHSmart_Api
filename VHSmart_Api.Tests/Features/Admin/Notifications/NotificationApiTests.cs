using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Features.Admin.Notifications;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;
using VHSmart_Api.Tests.Features.Auth;

namespace VHSmart_Api.Tests.Features.Admin.Notifications;

// Real pipeline (JWT -> [HasPermission(Dashboard, View)] -> MediatR) through AuthApiFactory,
// which runs the real RolePermissionService against the HasData seed: the VH Smart Admin role
// holds Dashboard/View, a role without permission rows answers 403.
public class NotificationApiTests
{
    private const string NotificationsRoute = "/api/admin/notifications";

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new AuthApiFactory();
        _ = await factory.SeedUserAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(NotificationsRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutDashboardPermission_ReturnsForbidden()
    {
        using var factory = new AuthApiFactory();
        var role = await factory.SeedRoleWithoutPermissionsAsync();
        var user = await factory.SeedUserAsync(roleId: role.Id);
        using var client = CreateClient(factory, user.Id, user.RoleId);

        var response = await client.GetAsync(NotificationsRoute);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOnlyMyRows()
    {
        using var factory = new AuthApiFactory();
        var me = await factory.SeedUserAsync();
        var other = await factory.SeedUserAsync("other@example.com");
        await SeedNotificationAsync(factory, me.Id, "MINE", isRead: false);
        await SeedNotificationAsync(factory, other.Id, "THEIRS", isRead: false);
        using var client = CreateClient(factory, me.Id, me.RoleId);

        var response = await client.GetAsync(NotificationsRoute);
        var grid = await response.Content.ReadFromJsonAsync<DataGridResponse<GetAllNotificationsResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(grid);
        var single = Assert.Single(grid.Data);
        Assert.Equal("MINE", single.Subject);
    }

    [Fact]
    public async Task UnreadCount_ReturnsMyUnreadOnly()
    {
        using var factory = new AuthApiFactory();
        var me = await factory.SeedUserAsync();
        var other = await factory.SeedUserAsync("other@example.com");
        await SeedNotificationAsync(factory, me.Id, "MINE UNREAD", isRead: false);
        await SeedNotificationAsync(factory, me.Id, "MINE READ", isRead: true);
        await SeedNotificationAsync(factory, other.Id, "THEIRS UNREAD", isRead: false);
        using var client = CreateClient(factory, me.Id, me.RoleId);

        var response = await client.GetAsync($"{NotificationsRoute}/unread-count");
        var count = await response.Content.ReadFromJsonAsync<GetUnreadCountResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(count);
        Assert.Equal(1, count.Count);
    }

    [Fact]
    public async Task MarkRead_OwnRow_ReturnsNoContentAndStores()
    {
        using var factory = new AuthApiFactory();
        var me = await factory.SeedUserAsync();
        var notification = await SeedNotificationAsync(factory, me.Id, "MINE");
        using var client = CreateClient(factory, me.Id, me.RoleId);

        var response = await client.PutAsync($"{NotificationsRoute}/{notification.Id}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.Notifications.SingleAsync(row => row.Id == notification.Id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task MarkRead_OtherUsersRow_ReturnsNotFound()
    {
        using var factory = new AuthApiFactory();
        var me = await factory.SeedUserAsync();
        var other = await factory.SeedUserAsync("other@example.com");
        var foreign = await SeedNotificationAsync(factory, other.Id, "THEIRS");
        using var client = CreateClient(factory, me.Id, me.RoleId);

        var response = await client.PutAsync($"{NotificationsRoute}/{foreign.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        var stored = await db.Notifications.SingleAsync(row => row.Id == foreign.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task MarkRead_UnknownId_ReturnsNotFound()
    {
        using var factory = new AuthApiFactory();
        var me = await factory.SeedUserAsync();
        using var client = CreateClient(factory, me.Id, me.RoleId);

        var response = await client.PutAsync($"{NotificationsRoute}/{Guid.NewGuid()}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateClient(AuthApiFactory factory, Guid userId, Guid roleId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            factory.CreateTokenFor(
                new UserEntity { Id = userId, RoleId = roleId },
                Guid.NewGuid()));
        return client;
    }

    private static async Task<NotificationEntity> SeedNotificationAsync(
        AuthApiFactory factory,
        Guid userId,
        string subject,
        bool isRead = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();
        await db.Database.EnsureCreatedAsync();

        var notification = new NotificationEntity
        {
            UserId = userId,
            Subject = subject,
            Message = $"{subject} message.",
            IsRead = isRead
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }
}
