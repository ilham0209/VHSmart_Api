using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.Finding.FindingTestData;

namespace VHSmart_Api.Tests.Features.Audit.Finding;

public class UpdateFindingTests
{
    private static UpdateFindingHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_StoresChangesAndRebuildsTheLinkSet()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var keepReco = await SeedRecommendationAsync(db, CompanyA, name: "Keep rule");
        var dropReco = await SeedRecommendationAsync(db, CompanyA, name: "Drop rule");
        var addReco = await SeedRecommendationAsync(db, CompanyA, name: "Add rule");
        var findingId = await SeedFindingAsync(
            db, CompanyA, name: "Old name", findingCode: "F111");
        await SeedFindingLinkAsync(db, CompanyA, findingId, keepReco);
        await SeedFindingLinkAsync(db, CompanyA, findingId, dropReco);

        var response = await Handler(db, user).Handle(
            new UpdateFindingCommand(
                findingId, "New name", "F222", "Updated", [keepReco, addReco]),
            CancellationToken.None);

        var stored = await db.Findings.SingleAsync();
        Assert.Equal("New name", stored.Name);
        Assert.Equal("F222", stored.FindingCode);
        Assert.Equal("Updated", stored.Description);
        // CompanyId is never editable - the row stays with its company (spec 3.3).
        Assert.Equal(CompanyA, stored.CompanyId);

        // The kept link survives untouched; the dropped one is soft-deleted (unlink frees
        // the UQ pair); the new one is inserted live - one save.
        var links = await db.FindingRecommendations.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(3, links.Count);
        var kept = links.Single(link => link.RecommendationId == keepReco);
        Assert.False(kept.IsDeleted);
        var dropped = links.Single(link => link.RecommendationId == dropReco);
        Assert.True(dropped.IsDeleted);
        var added = links.Single(link => link.RecommendationId == addReco);
        Assert.False(added.IsDeleted);

        Assert.Equal(stored.Id, response.Id);
        Assert.Equal(
            new[] { addReco, keepReco }.OrderBy(id => id),
            response.Recommendations.OrderBy(id => id));
    }

    [Fact]
    public async Task Handle_RelinkingTheSamePairAfterUnlink_InsertsALiveRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var findingId = await SeedFindingAsync(db, CompanyA);
        await SeedFindingLinkAsync(db, CompanyA, findingId, reco);

        // Unlink, then link again in a second edit: the filtered UQ pair only counts live
        // rows, so the soft-deleted link does not block the re-insert.
        await Handler(db, user).Handle(
            new UpdateFindingCommand(findingId, "Finding", "F111", null, []),
            CancellationToken.None);
        await Handler(db, user).Handle(
            new UpdateFindingCommand(findingId, "Finding", "F111", null, [reco]),
            CancellationToken.None);

        var live = await db.FindingRecommendations.ToListAsync();
        Assert.Single(live);
        Assert.Equal(reco, live[0].RecommendationId);
    }

    [Fact]
    public async Task Handle_EmptyRecommendations_RemovesEveryLiveLink()
    {
        // The pipeline validator rejects [] with 400 before the handler runs; called
        // directly the handler performs the requested rebuild - zero live links (the
        // >= 1 rule lives in the validator, spec 14.4 [CONFIRMED / MANUAL]).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var findingId = await SeedFindingAsync(db, CompanyA);
        await SeedFindingLinkAsync(db, CompanyA, findingId, reco);

        await Handler(db, user).Handle(
            new UpdateFindingCommand(findingId, "Finding", "F111", null, []),
            CancellationToken.None);

        Assert.Empty(await db.FindingRecommendations.ToListAsync());
    }

    [Fact]
    public async Task Handle_DuplicateCodeOfAnotherRow_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        await SeedFindingAsync(db, CompanyA, findingCode: "F111");
        var otherId = await SeedFindingAsync(db, CompanyA, findingCode: "F222");

        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler(db, user).Handle(
                new UpdateFindingCommand(otherId, "Other", "F111", null, [reco]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameCodeOnTheRowItself_IsAllowed()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var id = await SeedFindingAsync(db, CompanyA, findingCode: "F111");

        var response = await Handler(db, user).Handle(
            new UpdateFindingCommand(id, "Finding", "F111", null, [reco]),
            CancellationToken.None);

        Assert.Equal("F111", response.FindingCode);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateFindingCommand(
                    Guid.NewGuid(), "Name", "F111", null, []),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedFindingAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateFindingCommand(foreignId, "Name", "F111", null, []),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingId_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new UpdateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateFindingCommand(Guid.Empty, "Name", "F111", null, []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "At least one recommendation is required.");
    }

    [Fact]
    public async Task Validator_MissingFindingCode_Fails()
    {
        var user = UserA();
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        var validator = new UpdateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateFindingCommand(Guid.NewGuid(), "Name", string.Empty, null, []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Finding Code is required.");
    }

    [Fact]
    public async Task Validator_LiveOwnCompanyRecommendation_Passes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reco = await SeedRecommendationAsync(db, CompanyA);
        var validator = new UpdateFindingValidator(db, user);

        var result = await validator.ValidateAsync(
            new UpdateFindingCommand(
                Guid.NewGuid(), "Portion sizes are consistent.", "F111", null, [reco]));

        Assert.True(result.IsValid);
    }
}
