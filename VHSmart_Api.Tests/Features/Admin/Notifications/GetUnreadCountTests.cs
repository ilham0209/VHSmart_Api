using VHSmart_Api.Features.Admin.Notifications;

namespace VHSmart_Api.Tests.Features.Admin.Notifications;

public class GetUnreadCountTests
{
    [Fact]
    public async Task Handle_CountsMyUnreadOnly()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        await NotificationsTestData.AddNotificationAsync(db, recipientId, "ONE", "unread");
        await NotificationsTestData.AddNotificationAsync(db, recipientId, "TWO", "unread");
        await NotificationsTestData.AddNotificationAsync(db, recipientId, "THREE", "already read", isRead: true);
        await NotificationsTestData.AddNotificationAsync(db, Guid.NewGuid(), "OTHERS", "not mine");

        var response = await new GetUnreadCountHandler(db, caller)
            .Handle(new GetUnreadCountQuery(), default);

        Assert.Equal(2, response.Count);
    }

    [Fact]
    public async Task Handle_AllReadOrNone_ReturnsZero()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        var empty = await new GetUnreadCountHandler(db, caller)
            .Handle(new GetUnreadCountQuery(), default);
        Assert.Equal(0, empty.Count);

        await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "READ", "seen", isRead: true);

        var allRead = await new GetUnreadCountHandler(db, caller)
            .Handle(new GetUnreadCountQuery(), default);
        Assert.Equal(0, allRead.Count);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotCounted()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var notification = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "GONE", "unread but deleted");
        notification.IsDeleted = true;
        await db.SaveChangesAsync();

        var response = await new GetUnreadCountHandler(db, caller)
            .Handle(new GetUnreadCountQuery(), default);

        Assert.Equal(0, response.Count);
    }
}
