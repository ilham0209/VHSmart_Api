using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Account.Subscription;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Account.Subscription;

public class GetSubscriptionTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task Handle_LatestPeriod_ReturnsPackageExpiryAndRenewal()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        // Two periods: the latest End Date wins (spec 6.4 - Next Renewal = last End Date).
        AddSubscription(db, CompanyId, package.Id, new DateTime(2031, 4, 23), new DateTime(2032, 4, 22));
        AddSubscription(db, CompanyId, package.Id, new DateTime(2032, 4, 23), new DateTime(2033, 4, 22));
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Equal("ADC", response.PackageCode);
        Assert.Equal("Advanced", response.PackageName);
        // Expiry = End Date - 1 day, Next Renewal = End Date (spec 6.4, 13).
        Assert.Equal(new DateTime(2033, 4, 21), response.ExpiryDate);
        Assert.Equal(new DateTime(2033, 4, 22), response.NextRenewalDate);
        Assert.False(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_OngoingPeriod_ReturnsBlankDatesWithFlag()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "PLA");
        AddSubscription(db, CompanyId, package.Id, new DateTime(2026, 1, 1), new DateTime(9999, 12, 31));
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Equal("PLA", response.PackageCode);
        Assert.Null(response.ExpiryDate);
        Assert.Null(response.NextRenewalDate);
        Assert.True(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_NoPeriods_ReturnsEmptyResponse()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Null(response.PackageCode);
        Assert.Null(response.PackageName);
        Assert.Null(response.ExpiryDate);
        Assert.Null(response.NextRenewalDate);
        Assert.False(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_AnotherCompanysPeriod_IsNotVisible()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, Guid.NewGuid(), package.Id, new DateTime(2030, 1, 1), new DateTime(2031, 1, 1));
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Null(response.PackageCode);
        Assert.False(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_ViewAllCaller_StillReadsOwnCompanyOnly()
    {
        var db = await CreateDbAsync(CompanyId, viewAll: true);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        // The tenant filter is wide open for a ViewAllCompanies caller; the handler must
        // still answer from the JWT company, never from somebody else's rows.
        AddSubscription(db, Guid.NewGuid(), package.Id, new DateTime(2030, 1, 1), new DateTime(2031, 1, 1));
        var handler = new GetSubscriptionHandler(
            db, new TestCurrentUser(user.Id.ToString(), CompanyId, viewAllCompanies: true));

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Null(response.PackageCode);
        Assert.False(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_SoftDeletedPeriod_IsIgnored()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, CompanyId, package.Id, new DateTime(2030, 1, 1), new DateTime(2031, 1, 1), isDeleted: true);
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionQuery(), CancellationToken.None);

        Assert.Null(response.PackageCode);
        Assert.False(response.IsOngoing);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var db = await CreateDbAsync(CompanyId);
        var handler = new GetSubscriptionHandler(
            db, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyId));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetSubscriptionQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db, isActive: false);
        var handler = CreateHandler(db, user, CompanyId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new GetSubscriptionQuery(), CancellationToken.None));
    }

    // The tenant filter is bound to the context instance at construction, so the test's
    // current user has to be supplied here - the handler receives its own for identity.
    private static async Task<TestableVHSmartDbContext> CreateDbAsync(
        Guid companyId,
        bool viewAll = false)
    {
        var db = TestDbFactory.Create(
            TestDbFactory.NewDatabaseName(),
            new TestCurrentUser(string.Empty, companyId, viewAllCompanies: viewAll));
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static GetSubscriptionHandler CreateHandler(
        TestableVHSmartDbContext db,
        UserEntity user,
        Guid companyId) =>
        new(db, new TestCurrentUser(user.Id.ToString(), companyId, user.RoleId));

    private static UserEntity AddUser(TestableVHSmartDbContext db, bool isActive = true)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = $"subscription-{Guid.NewGuid():N}@example.com",
            IsActive = isActive,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static void AddSubscription(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid packageId,
        DateTime startDate,
        DateTime endDate,
        bool isDeleted = false)
    {
        db.CompanySubscriptions.Add(new CompanySubscriptionEntity
        {
            CompanyId = companyId,
            PackageId = packageId,
            EntryType = SubscriptionEntryType.New,
            DurationMonths = 12,
            StartDate = startDate,
            EndDate = endDate,
            IsDeleted = isDeleted
        });
        db.SaveChanges();
    }
}
