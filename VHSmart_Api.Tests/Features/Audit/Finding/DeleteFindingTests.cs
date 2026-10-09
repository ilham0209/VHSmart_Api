using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class DeleteFindingTests
{
    private static DeleteFindingHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user, new NotificationService(db));

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesTheFindingAndItsLiveLinks()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var findingId = await SeedFindingAsync(db, CompanyA);
        await SeedFindingLinkAsync(db, CompanyA, findingId, reco);

        await Handler(db, user).Handle(
            new DeleteFindingCommand(findingId), CancellationToken.None);

        // Soft delete only - no physical DELETE (CodingRules 7.1); the links belong to the
        // finding and must not outlive it as orphans.
        var finding = await db.Findings.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == findingId);
        Assert.True(finding.IsDeleted);
        var link = await db.FindingRecommendations.IgnoreQueryFilters()
            .SingleAsync(row => row.FindingId == findingId);
        Assert.True(link.IsDeleted);
        Assert.Empty(await db.Findings.ToListAsync());
        Assert.Empty(await db.FindingRecommendations.ToListAsync());
    }

    [Fact]
    public async Task Handle_SoftDelete_SendsInAppNotificationToActiveCompanyUsersExceptTheActor()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var findingId = await SeedFindingAsync(db, CompanyA, findingCode: "Portion sizes");
        // Two active members of CompanyA - the first IS the actor (its id is the JWT's).
        var actorId = Guid.Parse(user.UserId);
        await SeedUserAsync(db, CompanyA, isActive: true, userId: actorId);
        var recipientId = await SeedUserAsync(db, CompanyA);
        // Inactive member and an active member of another company must not be notified.
        await SeedUserAsync(db, CompanyA, isActive: false);
        await SeedUserAsync(db, CompanyB);

        await Handler(db, user).Handle(
            new DeleteFindingCommand(findingId), CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        var notification = Assert.Single(notifications);
        Assert.Equal(recipientId, notification.UserId);
        Assert.Equal(CompanyA, notification.CompanyId);
        Assert.Equal(DeleteFindingHandler.NotificationSubject, notification.Subject);
        Assert.Contains("Portion sizes", notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new DeleteFindingCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedFindingAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new DeleteFindingCommand(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedFindingAsync(db, CompanyA);
        await Handler(db, user).Handle(
            new DeleteFindingCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new DeleteFindingCommand(id), CancellationToken.None));
    }
}
