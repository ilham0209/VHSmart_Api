using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.Recommendation.RecommendationTestData;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class GetRecommendationByIdTests
{
    [Fact]
    public async Task Handle_ExistingRow_ReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRecommendationAsync(
            db, CompanyA, name: "Santan Berkualiti", recommendationCode: "R111",
            description: "Kelapa berkualiti");

        var response = await new GetRecommendationByIdHandler(db).Handle(
            new GetRecommendationByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal("Santan Berkualiti", response.Name);
        Assert.Equal("R111", response.RecommendationCode);
        Assert.Equal("Kelapa berkualiti", response.Description);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRecommendationByIdHandler(db).Handle(
                new GetRecommendationByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedRecommendationAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRecommendationByIdHandler(db).Handle(
                new GetRecommendationByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRecommendationAsync(db, CompanyA);
        await new DeleteRecommendationHandler(db).Handle(
            new DeleteRecommendationCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRecommendationByIdHandler(db).Handle(
                new GetRecommendationByIdQuery(id), CancellationToken.None));
    }
}
