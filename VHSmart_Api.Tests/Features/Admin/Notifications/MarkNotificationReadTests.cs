using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Notifications;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Notifications;

public class MarkNotificationReadTests
{
    [Fact]
    public async Task Handle_OwnUnreadNotification_SetsIsRead()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var notification = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "SUBJECT", "message");

        await new MarkNotificationReadHandler(db, caller)
            .Handle(new MarkNotificationReadCommand(notification.Id), default);

        var stored = await db.Notifications.SingleAsync(row => row.Id == notification.Id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task Handle_AlreadyRead_IsANoOp()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var notification = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "SUBJECT", "message", isRead: true);

        await new MarkNotificationReadHandler(db, caller)
            .Handle(new MarkNotificationReadCommand(notification.Id), default);

        var stored = await db.Notifications.SingleAsync(row => row.Id == notification.Id);
        Assert.True(stored.IsRead);
        // No second save: the repeated bell click must not re-stamp the row.
        Assert.Null(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_OtherUsersNotification_ThrowsNotFoundAndLeavesRow()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var foreign = await NotificationsTestData.AddNotificationAsync(
            db, Guid.NewGuid(), "THEIRS", "not mine");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarkNotificationReadHandler(db, caller)
                .Handle(new MarkNotificationReadCommand(foreign.Id), default));

        var stored = await db.Notifications.SingleAsync(row => row.Id == foreign.Id);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task Handle_SoftDeletedNotification_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var notification = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "GONE", "deleted row");
        notification.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarkNotificationReadHandler(db, caller)
                .Handle(new MarkNotificationReadCommand(notification.Id), default));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarkNotificationReadHandler(db, caller)
                .Handle(new MarkNotificationReadCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public void Validator_MissingId_Fails()
    {
        var validator = new MarkNotificationReadValidator();

        var result = validator.Validate(new MarkNotificationReadCommand(Guid.Empty));

        Assert.False(result.IsValid);
    }
}
