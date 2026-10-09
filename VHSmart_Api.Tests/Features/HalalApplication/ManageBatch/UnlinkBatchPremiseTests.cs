using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class UnlinkBatchPremiseTests
{
    [Fact]
    public async Task Handle_LinkedPremise_SoftDeletesTheRowAndFreesThePair()
    {
        // AppBatchPremises has no MappingStatus (Database.md 10): unlink IS a soft delete and
        // the filtered unique index frees (BatchId, PremiseId) for a re-associate.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);
        var linkId = await SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);

        await new UnlinkBatchPremiseHandler(db, user)
            .Handle(new UnlinkBatchPremiseCommand(batchId, linkId), CancellationToken.None);

        var stored = await db.BatchPremises.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.BatchPremises.ToArrayAsync());

        // The slot is free again: the same pair links as a NEW row (the old one stays soft
        // deleted, so IgnoreQueryFilters sees both and the filter sees the live one).
        var relinked = await LinkPremiseAgainAsync(db, batchId, premiseId);
        Assert.NotEqual(linkId, relinked);
        Assert.Equal(2, await db.BatchPremises.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.BatchPremises.CountAsync());
    }

    private static async Task<Guid> LinkPremiseAgainAsync(
        TestableVHSmartDbContext db,
        Guid batchId,
        Guid premiseId)
    {
        var row = new BatchPremiseEntity
        {
            CompanyId = CompanyA,
            BatchId = batchId,
            PremiseId = premiseId
        };
        db.BatchPremises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    [Fact]
    public async Task Handle_UnknownLinkId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchPremiseHandler(db, user)
                .Handle(
                    new UnlinkBatchPremiseCommand(batchId, Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_LinkOfAnotherBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await FoodPremiseSchemeIdAsync(db);
        var first = await SeedBatchAsync(db, CompanyA, name: "Batch One", schemeId: schemeId);
        var second = await SeedBatchAsync(db, CompanyA, name: "Batch Two", schemeId: schemeId);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);
        var linkOfFirst = await SeedBatchPremiseAsync(db, CompanyA, first, premiseId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchPremiseHandler(db, user)
                .Handle(
                    new UnlinkBatchPremiseCommand(second, linkOfFirst),
                    CancellationToken.None));

        Assert.False((await db.BatchPremises.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchPremiseHandler(db, user)
                .Handle(
                    new UnlinkBatchPremiseCommand(Guid.NewGuid(), Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BatchOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignBatch = await SeedBatchAsync(
            db, CompanyB, schemeId: await FoodPremiseSchemeIdAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchPremiseHandler(db, user)
                .Handle(
                    new UnlinkBatchPremiseCommand(foreignBatch, Guid.NewGuid()),
                    CancellationToken.None));
    }
}
