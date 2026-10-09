using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.Recommendation.RecommendationTestData;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class DeleteRecommendationTests
{
    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRecommendationAsync(db, CompanyA);

        await new DeleteRecommendationHandler(db).Handle(
            new DeleteRecommendationCommand(id), CancellationToken.None);

        // Soft delete only - no physical DELETE (CodingRules 7.1).
        var stored = await db.Recommendations.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == id);
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.Recommendations.ToListAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRecommendationHandler(db).Handle(
                new DeleteRecommendationCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedRecommendationAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRecommendationHandler(db).Handle(
                new DeleteRecommendationCommand(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRecommendationAsync(db, CompanyA);
        await new DeleteRecommendationHandler(db).Handle(
            new DeleteRecommendationCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteRecommendationHandler(db).Handle(
                new DeleteRecommendationCommand(id), CancellationToken.None));
    }
}
