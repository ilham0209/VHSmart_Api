using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class GetFindingByIdTests
{
    [Fact]
    public async Task Handle_ExistingRow_ReturnsTheDetailAndTheLiveRecommendationIds()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco1 = await SeedRecommendationAsync(db, CompanyA, name: "Alfa rule");
        var reco2 = await SeedRecommendationAsync(db, CompanyA, name: "Zulu rule");
        var id = await SeedFindingAsync(
            db, CompanyA,
            name: "Portion sizes are consistent with the menu descriptions.",
            findingCode: "Portion sizes",
            description: "Long audit statement");
        await SeedFindingLinkAsync(db, CompanyA, id, reco2);
        await SeedFindingLinkAsync(db, CompanyA, id, reco1);

        var response = await new GetFindingByIdHandler(db, user).Handle(
            new GetFindingByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal(
            "Portion sizes are consistent with the menu descriptions.", response.Name);
        Assert.Equal("Portion sizes", response.FindingCode);
        Assert.Equal("Long audit statement", response.Description);
        Assert.Null(response.ModifiedDate);
        Assert.Equal(
            new[] { reco1, reco2 }.OrderBy(value => value),
            response.RecommendationIds.OrderBy(value => value));
    }

    [Fact]
    public async Task Handle_UnlinkedRecommendation_IsNotReturned()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var keepReco = await SeedRecommendationAsync(db, CompanyA);
        var dropReco = await SeedRecommendationAsync(db, CompanyA);
        var id = await SeedFindingAsync(db, CompanyA);
        await SeedFindingLinkAsync(db, CompanyA, id, keepReco);
        await SeedFindingLinkAsync(db, CompanyA, id, dropReco);

        await new UpdateFindingHandler(db, user).Handle(
            new UpdateFindingCommand(id, "Finding", "F111", null, [keepReco]),
            CancellationToken.None);
        var response = await new GetFindingByIdHandler(db, user).Handle(
            new GetFindingByIdQuery(id), CancellationToken.None);

        // The dropped link was soft-deleted (unlink) and must not reappear as state.
        Assert.Equal([keepReco], response.RecommendationIds);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetFindingByIdHandler(db, user).Handle(
                new GetFindingByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedFindingAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetFindingByIdHandler(db, user).Handle(
                new GetFindingByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedFindingAsync(db, CompanyA);
        await new DeleteFindingHandler(db, user, new NotificationService(db))
            .Handle(new DeleteFindingCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetFindingByIdHandler(db, user).Handle(
                new GetFindingByIdQuery(id), CancellationToken.None));
    }
}
