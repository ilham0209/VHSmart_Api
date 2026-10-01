using VHSmart_Api.Features.Admin.Notifications;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Notifications;

public class GetAllNotificationsTests
{
    [Fact]
    public async Task Handle_ReturnsMyRowsOnly_NewestFirst()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        var older = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "OLDER", "first message");
        var newer = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "NEWER", "second message");
        await NotificationsTestData.AddNotificationAsync(
            db, Guid.NewGuid(), "SOMEONE ELSE", "not mine");

        // SaveChanges stamps identical timestamps within one tick; the default order is
        // newest first, so the fixture backdates one row (Modified leaves SysDateCreated alone).
        older.SysDateCreated = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var grid = await new GetAllNotificationsHandler(db, caller)
            .Handle(new GetAllNotificationsQuery(), default);

        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { newer.Id, older.Id }, grid.Data.Select(row => row.Id).ToArray());
        Assert.All(grid.Data, row => Assert.False(row.IsRead));
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersBySubjectAndMessage()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "Training created", "Training was added.");
        await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "Batch created", "Batch was added.");
        await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "Report ready", "The batch report is ready.");

        var grid = await new GetAllNotificationsHandler(db, caller).Handle(
            new GetAllNotificationsQuery
            {
                Request = new() { SearchTerm = "batch" }
            },
            default);

        Assert.Equal(2, grid.TotalRecords);
        Assert.Contains(grid.Data, row => row.Subject == "Batch created");
        Assert.Contains(grid.Data, row => row.Subject == "Report ready");
    }

    [Fact]
    public async Task Handle_ClientSort_OverridesDefaultOrder()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        await NotificationsTestData.AddNotificationAsync(db, recipientId, "BETA", "b");
        await NotificationsTestData.AddNotificationAsync(db, recipientId, "ALPHA", "a");

        var grid = await new GetAllNotificationsHandler(db, caller).Handle(
            new GetAllNotificationsQuery
            {
                Request = new() { SortBy = "Subject", SortDescending = false }
            },
            default);

        Assert.Equal(new[] { "ALPHA", "BETA" }, grid.Data.Select(row => row.Subject).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsHidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var recipientId = Guid.Parse(caller.UserId);
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);
        var notification = await NotificationsTestData.AddNotificationAsync(
            db, recipientId, "GONE", "deleted row");
        notification.IsDeleted = true;
        await db.SaveChangesAsync();

        var grid = await new GetAllNotificationsHandler(db, caller)
            .Handle(new GetAllNotificationsQuery(), default);

        Assert.Empty(grid.Data);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_NoRows_ReturnsEmptyGrid()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller();
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        var grid = await new GetAllNotificationsHandler(db, caller)
            .Handle(new GetAllNotificationsQuery(), default);

        Assert.Empty(grid.Data);
        Assert.Equal(0, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_CallerWithoutGuidUserId_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = NotificationsTestData.Caller(userId: "not-a-guid");
        var db = await NotificationsTestData.CreateDbAsync(databaseName, caller);

        // Fail closed: a caller whose sub claim is not a Guid sees nothing, not everything.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAllNotificationsHandler(db, caller)
                .Handle(new GetAllNotificationsQuery(), default));
    }
}
