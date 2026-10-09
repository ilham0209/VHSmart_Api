using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Recommendation;
using static VHSmart_Api.Tests.Features.Audit.Recommendation.RecommendationTestData;

namespace VHSmart_Api.Tests.Features.Audit.Recommendation;

public class CreateRecommendationTests
{
    private static CreateRecommendationHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesRowForTheJwtCompanyAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new CreateRecommendationCommand(
                "Santan Berkualiti", "R111",
                "Sila Pastikan Kelapa Yang Di Ambil Adalah Kelapa Yang Berkualiti"),
            CancellationToken.None);

        var stored = await db.Recommendations.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("Santan Berkualiti", stored.Name);
        Assert.Equal("R111", stored.RecommendationCode);
        Assert.False(stored.IsDeleted);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("Santan Berkualiti", response.Name);
        Assert.Equal("R111", response.RecommendationCode);
        Assert.Equal(
            "Sila Pastikan Kelapa Yang Di Ambil Adalah Kelapa Yang Berkualiti",
            response.Description);
    }

    [Fact]
    public async Task Handle_DuplicateCodeOrName_IsAllowed_NoDuplicateRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = new CreateRecommendationCommand("Santan Berkualiti", "R111", null);
        await Handler(db, user).Handle(command, CancellationToken.None);

        // Spec 14.3: codes are free text and no uniqueness was ever seen - a second row
        // with the same name AND code must go through.
        var second = await Handler(db, user).Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.Equal(2, await db.Recommendations.CountAsync());
    }

    [Fact]
    public void Validator_MissingName_Fails()
    {
        var validator = new CreateRecommendationValidator();

        var result = validator.Validate(
            new CreateRecommendationCommand(string.Empty, "R111", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name is required.");
    }

    [Fact]
    public void Validator_NameLongerThan2000_Fails()
    {
        var validator = new CreateRecommendationValidator();

        var result = validator.Validate(
            new CreateRecommendationCommand(new string('x', 2001), "R111", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name must be 2000 characters or fewer.");
    }

    [Fact]
    public void Validator_MissingRecommendationCode_Fails()
    {
        var validator = new CreateRecommendationValidator();

        var result = validator.Validate(
            new CreateRecommendationCommand("Santan Berkualiti", string.Empty, null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Recommendation Code is required.");
    }

    [Fact]
    public void Validator_RecommendationCodeLongerThan50_Fails()
    {
        var validator = new CreateRecommendationValidator();

        var result = validator.Validate(
            new CreateRecommendationCommand("Santan Berkualiti", new string('x', 51), null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Recommendation Code must be 50 characters or fewer.");
    }

    [Fact]
    public void Validator_DescriptionLongerThan1000_Fails()
    {
        var validator = new CreateRecommendationValidator();

        var result = validator.Validate(
            new CreateRecommendationCommand(
                "Santan Berkualiti", "R111", new string('x', 1001)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public void Validator_FreeTextCode_Passes()
    {
        var validator = new CreateRecommendationValidator();

        // Spec 14.3 samples: "recoI", "12345", "r3" are all valid codes.
        Assert.True(validator.Validate(
            new CreateRecommendationCommand("Name", "recoI", null)).IsValid);
        Assert.True(validator.Validate(
            new CreateRecommendationCommand("Name", "12345", null)).IsValid);
        Assert.True(validator.Validate(
            new CreateRecommendationCommand("Name", "r3", null)).IsValid);
    }
}
