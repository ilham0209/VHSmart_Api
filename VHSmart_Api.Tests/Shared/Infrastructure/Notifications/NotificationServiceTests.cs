using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Notifications;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Notifications;

public class NotificationServiceTests
{
    private static readonly Guid TargetUserId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task PublishAsync_ValidNotification_StoresUnreadRowForTargetUser()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await service.PublishAsync(TargetUserId, "Batch created", "Batch ABC was created");

        var stored = Assert.Single(db.Notifications);
        Assert.Equal(TargetUserId, stored.UserId);
        Assert.Equal("Batch created", stored.Subject);
        Assert.Equal("Batch ABC was created", stored.Message);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task PublishAsync_WithCompanyAndLink_StoresThem()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await service.PublishAsync(
            TargetUserId, "Premise deleted", "Premise X was deleted", "/premises", CompanyId);

        var stored = Assert.Single(db.Notifications);
        Assert.Equal(CompanyId, stored.CompanyId);
        Assert.Equal("/premises", stored.LinkUrl);
    }

    [Fact]
    public async Task PublishAsync_StampedWithCallingUserNotTargetUser()
    {
        var actor = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyId);
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), actor);
        var service = new NotificationService(db);

        await service.PublishAsync(TargetUserId, "Data saved successfully", "Saved");

        var stored = Assert.Single(db.Notifications);
        Assert.Equal(actor.UserId, stored.SysUserCreated);
        Assert.Equal(TargetUserId, stored.UserId);
    }

    [Fact]
    public async Task PublishManyAsync_SeveralUsers_CreatesOneRowPerUser()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);
        Guid[] userIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        await service.PublishManyAsync(userIds, "Training updated", "Training T was updated");

        Assert.Equal(userIds.Length, db.Notifications.Count());
        Assert.Equal(
            userIds.OrderBy(id => id),
            db.Notifications.Select(row => row.UserId).ToList().OrderBy(id => id));
    }

    [Fact]
    public async Task PublishManyAsync_NoUsers_StoresNothing()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await service.PublishManyAsync([], "Training updated", "Training T was updated");

        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task PublishAsync_EmptySubject_Throws()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PublishAsync(TargetUserId, " ", "Message"));
    }

    [Fact]
    public async Task PublishAsync_EmptyMessage_Throws()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PublishAsync(TargetUserId, "Subject", string.Empty));
    }

    [Fact]
    public async Task PublishAsync_SubjectOverColumnLength_Throws()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PublishAsync(TargetUserId, new string('s', 201), "Message"));
    }

    [Fact]
    public async Task PublishAsync_MessageOverColumnLength_Throws()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PublishAsync(TargetUserId, "Subject", new string('m', 1001)));
    }

    [Fact]
    public async Task PublishAsync_LinkUrlOverColumnLength_Throws()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PublishAsync(TargetUserId, "Subject", "Message", new string('l', 301)));
    }

    [Fact]
    public async Task PublishAsync_ReadOnlyRowIsFoundByUnfilteredQuery()
    {
        // The soft-delete filter is what hides deleted rows; an unread one must come back,
        // because the bell count (C-03) is "my" unread rows.
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        var service = new NotificationService(db);
        await service.PublishAsync(TargetUserId, "Subject", "Message");

        var unread = await db.Notifications
            .Where(row => row.UserId == TargetUserId && !row.IsRead)
            .ToListAsync();

        Assert.Single(unread);
    }
}
