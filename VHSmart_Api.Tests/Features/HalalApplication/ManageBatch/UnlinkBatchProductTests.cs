using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class UnlinkBatchProductTests
{
    [Fact]
    public async Task Handle_ActiveRow_IsKeptAndFlippedToInactive()
    {
        // Database.md 10: unlink TOGGLES MappingStatus - never a delete, so the pair stays on
        // the table showing its status (the UnlinkProductIngredient stance).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedValidProductAsync(db, CompanyA);
        await SeedBatchProductAsync(db, CompanyA, batchId, productId);

        await new UnlinkBatchProductHandler(db, user)
            .Handle(
                new UnlinkBatchProductCommand(
                    batchId, (await db.BatchProducts.SingleAsync()).Id),
                CancellationToken.None);

        var stored = await db.BatchProducts.SingleAsync();
        Assert.False(stored.IsDeleted);
        Assert.Equal(BatchProductMappingStatus.Inactive, stored.MappingStatus);
    }

    [Fact]
    public async Task Handle_AlreadyInactiveRow_IsANoOp()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedValidProductAsync(db, CompanyA);
        await SeedBatchProductAsync(
            db, CompanyA, batchId, productId, BatchProductMappingStatus.Inactive);

        var linkId = (await db.BatchProducts.SingleAsync()).Id;
        await new UnlinkBatchProductHandler(db, user)
            .Handle(new UnlinkBatchProductCommand(batchId, linkId), CancellationToken.None);

        var stored = await db.BatchProducts.SingleAsync();
        Assert.Equal(BatchProductMappingStatus.Inactive, stored.MappingStatus);
    }

    [Fact]
    public async Task Handle_UnknownLinkId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchProductHandler(db, user)
                .Handle(
                    new UnlinkBatchProductCommand(batchId, Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_LinkOfAnotherBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var first = await SeedBatchAsync(db, CompanyA, name: "Batch One");
        var second = await SeedBatchAsync(db, CompanyA, name: "Batch Two");
        var productId = await SeedValidProductAsync(db, CompanyA);
        await SeedBatchProductAsync(db, CompanyA, first, productId);
        var linkOfFirst = (await db.BatchProducts.SingleAsync()).Id;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchProductHandler(db, user)
                .Handle(new UnlinkBatchProductCommand(second, linkOfFirst), CancellationToken.None));

        Assert.Equal(
            BatchProductMappingStatus.Active,
            (await db.BatchProducts.SingleAsync()).MappingStatus);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchProductHandler(db, user)
                .Handle(
                    new UnlinkBatchProductCommand(Guid.NewGuid(), Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BatchOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignBatch = await SeedBatchAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UnlinkBatchProductHandler(db, user)
                .Handle(
                    new UnlinkBatchProductCommand(foreignBatch, Guid.NewGuid()),
                    CancellationToken.None));
    }
}
