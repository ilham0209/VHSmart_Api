using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Account.Subscription;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Account.Subscription;

public class GetSubscriptionHistoryTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task Handle_DefaultOrder_IsNewestFirstWithContinuousPageNumbers()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, CompanyId, package.Id, "NEW : VH SMART - Advanced (1 Year)",
            new DateTime(2032, 4, 23), new DateTime(2033, 4, 22));
        AddSubscription(db, CompanyId, package.Id, "RENEWAL : VH SMART - Advanced (1 Year)",
            new DateTime(2031, 4, 23), new DateTime(2032, 4, 22));
        AddSubscription(db, CompanyId, package.Id, "NEW : VH SMART - Advanced (1 Year)",
            new DateTime(2030, 4, 23), new DateTime(2031, 4, 22));
        var handler = CreateHandler(db, user, CompanyId);

        var pageTwo = await handler.Handle(
            new GetSubscriptionHistoryQuery { Request = new DataGridRequest { Page = 2, PageSize = 2 } },
            CancellationToken.None);

        // Newest first by default; "No." counts rows in the whole list, not on the page.
        var row = Assert.Single(pageTwo.Data);
        Assert.Equal(3, row.No);
        Assert.Equal(new DateTime(2030, 4, 23), row.StartDate);
        Assert.Equal(3, pageTwo.TotalRecords);

        var pageOne = await handler.Handle(
            new GetSubscriptionHistoryQuery { Request = new DataGridRequest { Page = 1, PageSize = 2 } },
            CancellationToken.None);
        Assert.Equal([1, 2], pageOne.Data.Select(x => x.No));
        Assert.Equal(new DateTime(2032, 4, 23), pageOne.Data.First().StartDate);
    }

    [Fact]
    public async Task Handle_Search_FiltersByLabelOrPackageName()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var advanced = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        var lite = await db.SubscriptionPackages.SingleAsync(x => x.Code == "LTE");
        AddSubscription(db, CompanyId, advanced.Id, "RENEWAL : VH SMART - Advanced (1 Year)",
            new DateTime(2031, 1, 1), new DateTime(2032, 1, 1));
        AddSubscription(db, CompanyId, lite.Id, "NEW : VH SMART - Lite (1 Year)",
            new DateTime(2032, 1, 1), new DateTime(2033, 1, 1));
        var handler = CreateHandler(db, user, CompanyId);

        var byLabel = await handler.Handle(
            new GetSubscriptionHistoryQuery { Request = new DataGridRequest { SearchTerm = "renewal" } },
            CancellationToken.None);
        Assert.Equal("RENEWAL : VH SMART - Advanced (1 Year)", Assert.Single(byLabel.Data).Label);

        var byPackageName = await handler.Handle(
            new GetSubscriptionHistoryQuery { Request = new DataGridRequest { SearchTerm = "lite" } },
            CancellationToken.None);
        Assert.Equal("Lite", Assert.Single(byPackageName.Data).PackageName);
    }

    [Fact]
    public async Task Handle_ClientSort_OverridesDefaultOrder()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, CompanyId, package.Id, null, new DateTime(2031, 1, 1), new DateTime(2032, 1, 1));
        AddSubscription(db, CompanyId, package.Id, null, new DateTime(2032, 1, 1), new DateTime(2033, 1, 1));
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(
            new GetSubscriptionHistoryQuery
            {
                Request = new DataGridRequest { SortBy = nameof(GetSubscriptionHistoryResponse.StartDate) }
            },
            CancellationToken.None);

        // Ascending on request, against the newest-first default.
        Assert.Equal(
            [new DateTime(2031, 1, 1), new DateTime(2032, 1, 1)],
            response.Data.Select(x => x.StartDate));
    }

    [Fact]
    public async Task Handle_AnotherCompanysRows_AreNotReturned()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, Guid.NewGuid(), package.Id, "OTHER", new DateTime(2030, 1, 1), new DateTime(2031, 1, 1));
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionHistoryQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_ViewAllCaller_StillScopedToOwnCompany()
    {
        var db = await CreateDbAsync(CompanyId, viewAll: true);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, Guid.NewGuid(), package.Id, "OTHER", new DateTime(2030, 1, 1), new DateTime(2031, 1, 1));
        var handler = new GetSubscriptionHistoryHandler(
            db, new TestCurrentUser(user.Id.ToString(), CompanyId, viewAllCompanies: true));

        var response = await handler.Handle(new GetSubscriptionHistoryQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_SoftDeletedPeriod_IsHidden()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, CompanyId, package.Id, "OLD", new DateTime(2030, 1, 1), new DateTime(2031, 1, 1), isDeleted: true);
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionHistoryQuery(), CancellationToken.None);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task Handle_DeletedPackage_KeepsRowWithEmptyPackageName()
    {
        var db = await CreateDbAsync(CompanyId);
        var user = AddUser(db);
        var package = await db.SubscriptionPackages.SingleAsync(x => x.Code == "ADC");
        AddSubscription(db, CompanyId, package.Id, "RENEWAL : VH SMART - Advanced (1 Year)",
            new DateTime(2030, 1, 1), new DateTime(2031, 1, 1));
        package.IsDeleted = true;
        db.SaveChanges();
        var handler = CreateHandler(db, user, CompanyId);

        var response = await handler.Handle(new GetSubscriptionHistoryQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("RENEWAL : VH SMART - Advanced (1 Year)", row.Label);
        Assert.Equal(string.Empty, row.PackageName);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var db = await CreateDbAsync(CompanyId);
        var handler = new GetSubscriptionHistoryHandler(
            db, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyId));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetSubscriptionHistoryQuery(), CancellationToken.None));
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

    private static GetSubscriptionHistoryHandler CreateHandler(
        TestableVHSmartDbContext db,
        UserEntity user,
        Guid companyId) =>
        new(db, new TestCurrentUser(user.Id.ToString(), companyId, user.RoleId));

    private static UserEntity AddUser(TestableVHSmartDbContext db)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = $"subscription-history-{Guid.NewGuid():N}@example.com",
            IsActive = true,
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
        string? label,
        DateTime startDate,
        DateTime endDate,
        bool isDeleted = false)
    {
        db.CompanySubscriptions.Add(new CompanySubscriptionEntity
        {
            CompanyId = companyId,
            PackageId = packageId,
            EntryType = SubscriptionEntryType.New,
            Label = label,
            DurationMonths = 12,
            StartDate = startDate,
            EndDate = endDate,
            IsDeleted = isDeleted
        });
        db.SaveChanges();
    }
}
