using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Subscriptions;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Subscriptions;

public class SubscriptionExpiryWarningProcessorTests
{
    // The job warns when End Date - 7 days = Today (D-11).
    private static readonly DateTime EndDate = new(2030, 6, 15);

    private static readonly DateTime Today = EndDate.AddDays(-7);

    [Fact]
    public async Task ProcessAsync_DuePeriod_WarnsActiveMembersAndStamps()
    {
        var db = await CreateDbAsync();
        var companyId = Guid.NewGuid();
        AddSubscription(db, companyId, EndDate);
        var member = AddUser(db, isActive: true);
        AddMembership(db, member.Id, companyId);
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        Assert.Equal(1, warned);
        var notification = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(member.Id, notification.UserId);
        Assert.Equal(companyId, notification.CompanyId);
        Assert.Equal(SubscriptionExpiryWarningProcessor.Subject, notification.Subject);
        // The user sees End Date - 1 day as the expiry date (spec 6.4, 13).
        Assert.Contains("2030-06-14", notification.Message);
        var row = Assert.Single(await db.CompanySubscriptions.IgnoreQueryFilters().ToListAsync());
        Assert.NotNull(row.ExpiryWarningSentAt);
    }

    [Fact]
    public async Task ProcessAsync_RunsTwice_WarnsOnlyOnce()
    {
        var db = await CreateDbAsync();
        var companyId = Guid.NewGuid();
        AddSubscription(db, companyId, EndDate);
        var member = AddUser(db, isActive: true);
        AddMembership(db, member.Id, companyId);
        var processor = CreateProcessor(db);

        var first = await processor.ProcessAsync(Today);
        var second = await processor.ProcessAsync(Today);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_EndDateSixDaysAway_NoWarning()
    {
        var db = await CreateDbAsync();
        AddSubscription(db, Guid.NewGuid(), Today.AddDays(6));
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        Assert.Equal(0, warned);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_EndDateEightDaysAway_NoWarning()
    {
        var db = await CreateDbAsync();
        AddSubscription(db, Guid.NewGuid(), Today.AddDays(8));
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        Assert.Equal(0, warned);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_SoftDeletedPeriod_IsSkipped()
    {
        var db = await CreateDbAsync();
        AddSubscription(db, Guid.NewGuid(), EndDate, isDeleted: true);
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        Assert.Equal(0, warned);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_InactiveUser_IsNotWarned()
    {
        var db = await CreateDbAsync();
        var companyId = Guid.NewGuid();
        AddSubscription(db, companyId, EndDate);
        var inactive = AddUser(db, isActive: false);
        AddMembership(db, inactive.Id, companyId);
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        // The period is still stamped (it was processed) but nobody received a bell row.
        Assert.Equal(1, warned);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_UserOfAnotherCompany_IsNotWarned()
    {
        var db = await CreateDbAsync();
        AddSubscription(db, Guid.NewGuid(), EndDate);
        var stranger = AddUser(db, isActive: true);
        AddMembership(db, stranger.Id, Guid.NewGuid());
        var processor = CreateProcessor(db);

        var warned = await processor.ProcessAsync(Today);

        Assert.Equal(1, warned);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    private static SubscriptionExpiryWarningProcessor CreateProcessor(TestableVHSmartDbContext db) =>
        new(db, new NotificationService(db));

    private static async Task<TestableVHSmartDbContext> CreateDbAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static void AddSubscription(
        TestableVHSmartDbContext db,
        Guid companyId,
        DateTime endDate,
        bool isDeleted = false)
    {
        var package = db.SubscriptionPackages.First(x => x.Code == "ADC");
        db.CompanySubscriptions.Add(new CompanySubscriptionEntity
        {
            CompanyId = companyId,
            PackageId = package.Id,
            EntryType = SubscriptionEntryType.New,
            DurationMonths = 12,
            StartDate = endDate.AddYears(-1).AddDays(1),
            EndDate = endDate,
            IsDeleted = isDeleted
        });
        db.SaveChanges();
    }

    private static UserEntity AddUser(TestableVHSmartDbContext db, bool isActive)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = $"warning-{Guid.NewGuid():N}@example.com",
            IsActive = isActive,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static void AddMembership(TestableVHSmartDbContext db, Guid userId, Guid companyId)
    {
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = userId,
            CompanyId = companyId,
            IsDefault = true
        });
        db.SaveChanges();
    }
}
