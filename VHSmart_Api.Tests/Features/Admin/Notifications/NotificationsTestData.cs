using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Notifications;

// Fixtures for the bell tests. Rows are inserted by hand: INotificationService is the
// production writer, but seeding a read-side fixture through it would only test the writer
// again (F-08 already covers it).
internal static class NotificationsTestData
{
    public static TestCurrentUser Caller(string? userId = null) =>
        new(userId ?? Guid.NewGuid().ToString(), Guid.NewGuid());

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(
        string databaseName,
        ICurrentUser? user = null)
    {
        var db = TestDbFactory.Create(databaseName, user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<NotificationEntity> AddNotificationAsync(
        VHSmartDbContext db,
        Guid userId,
        string subject,
        string message,
        bool isRead = false,
        string? linkUrl = null)
    {
        var notification = new NotificationEntity
        {
            UserId = userId,
            Subject = subject,
            Message = message,
            IsRead = isRead,
            LinkUrl = linkUrl
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }
}
