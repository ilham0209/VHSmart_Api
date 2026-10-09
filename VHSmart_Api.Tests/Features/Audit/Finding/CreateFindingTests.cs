using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class CreateFindingTests
{
    private static CreateFindingHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_CreatesRowAndLinksForTheJwtCompanyAndReturnsTheDetail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco1 = await SeedRecommendationAsync(db, CompanyA, name: "Alfa rule");
        var reco2 = await SeedRecommendationAsync(db, CompanyA, name: "Zulu rule");

        var response = await Handler(db, user).Handle(
            new CreateFindingCommand(
                "Portion sizes are consistent with the menu descriptions.",
                "Portion sizes",
                "Long audit statement",
                [reco1, reco2]),
            CancellationToken.None);

        var stored = await db.Findings.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("Portion sizes", stored.FindingCode);
        Assert.False(stored.IsDeleted);

        var links = await db.FindingRecommendations.ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link =>
        {
            Assert.Equal(CompanyA, link.CompanyId);
            Assert.Equal(stored.Id, link.FindingId);
        });

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("Long audit statement", response.Description);
        Assert.Equal(2, response.Recommendations.Count);
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        await SeedFindingAsync(db, CompanyA, findingCode: "F111");

        // Database.md 11 UQ (CompanyId, FindingCode) non-deleted: the handler answers 409
        // with a friendly message instead of letting the index fire.
        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new CreateFindingCommand("Another finding", "F111", null, [reco]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameCodeInAnotherCompany_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedFindingAsync(db, CompanyB, findingCode: "F111");
        var reco = await SeedRecommendationAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateFindingCommand("Own finding", "F111", null, [reco]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(2, await db.Findings.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_DuplicateRecommendationIds_CollapseToOneLinkRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);

        var response = await Handler(db, user).Handle(
            new CreateFindingCommand("Finding", "F111", null, [reco, reco]),
            CancellationToken.None);

        Assert.Single(response.Recommendations);
        Assert.Single(await db.FindingRecommendations.ToListAsync());
    }

    [Fact]
    public async Task Validator_RecommendationOfAnotherCompany_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignReco = await SeedRecommendationAsync(db, CompanyB);
        var validator = new CreateFindingValidator(db, user);

        // The checkbox table only ever offers the caller's own company rows (the options
        // endpoint scopes them); a foreign id is rejected before the handler runs.
        var result = await validator.ValidateAsync(
            new CreateFindingCommand("Finding", "F111", null, [foreignReco]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Recommendation not found.");
    }

    [Fact]
    public async Task Validator_MissingName_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand(string.Empty, "F111", null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task Validator_NameLongerThan2000_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand(new string('x', 2001), "F111", null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Name must be 2000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_MissingFindingCode_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand("Finding", string.Empty, null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding Code is required.");
    }

    [Fact]
    public async Task Validator_FindingCodeLongerThan100_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand("Finding", new string('x', 101), null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding Code must be 100 characters or fewer.");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan1000_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand(
                "Finding", "F111", new string('x', 1001), [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Description must be 1000 characters or fewer.");
    }

    [Fact]
    public async Task Validator_EmptyRecommendations_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand("Finding", "F111", null, []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "At least one recommendation is required.");
    }

    [Fact]
    public async Task Validator_UnknownRecommendation_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand("Finding", "F111", null, [Guid.NewGuid()]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Recommendation not found.");
    }

    [Fact]
    public async Task Validator_LiveOwnCompanyRecommendation_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var validator = new CreateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new CreateFindingCommand(
                "Portion sizes are consistent.", "Portion sizes", null, [reco]));

        Assert.True(result.IsValid);
    }
}
