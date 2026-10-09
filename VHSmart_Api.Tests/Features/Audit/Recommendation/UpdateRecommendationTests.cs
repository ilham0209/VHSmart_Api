using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.Recommendation.RecommendationTestData;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class UpdateRecommendationTests
{
    private static UpdateRecommendationHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StoresChangesAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedRecommendationAsync(db, CompanyA, name: "Old name");

        var response = await Handler(db, user).Handle(
            new UpdateRecommendationCommand(id, "New name", "R222", "Updated"),
            CancellationToken.None);

        var stored = await db.Recommendations.SingleAsync();
        Assert.Equal("New name", stored.Name);
        Assert.Equal("R222", stored.RecommendationCode);
        Assert.Equal("Updated", stored.Description);
        // CompanyId is never editable - the row stays with its company (spec 3.3).
        Assert.Equal(CompanyA, stored.CompanyId);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("New name", response.Name);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateRecommendationCommand(
                    Guid.NewGuid(), "Name", "R111", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedRecommendationAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateRecommendationCommand(foreignId, "Name", "R111", null),
                CancellationToken.None));
    }

    [Fact]
    public void Validator_MissingId_Fails()
    {
        var validator = new UpdateRecommendationValidator();

        var result = validator.Validate(
            new UpdateRecommendationCommand(Guid.Empty, "Name", "R111", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
    }

    [Fact]
    public void Validator_MissingRecommendationCode_Fails()
    {
        var validator = new UpdateRecommendationValidator();

        var result = validator.Validate(
            new UpdateRecommendationCommand(Guid.NewGuid(), "Name", string.Empty, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Recommendation Code is required.");
    }

    [Fact]
    public void Validator_NameLongerThan2000_Fails()
    {
        var validator = new UpdateRecommendationValidator();

        var result = validator.Validate(
            new UpdateRecommendationCommand(
                Guid.NewGuid(), new string('x', 2001), "R111", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name must be 2000 characters or fewer.");
    }

    [Fact]
    public void Validator_DescriptionLongerThan1000_Fails()
    {
        var validator = new UpdateRecommendationValidator();

        var result = validator.Validate(
            new UpdateRecommendationCommand(
                Guid.NewGuid(), "Name", "R111", new string('x', 1001)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }
}
