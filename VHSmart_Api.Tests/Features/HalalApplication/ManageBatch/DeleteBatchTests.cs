using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class DeleteBatchTests
{
    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesItAndKeepsItsLinks()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var productId = await SeedUnlinkedProductAsync(db, CompanyA);
        await SeedBatchProductAsync(db, CompanyA, batchId, productId);

        // The fixture seeded the link through this context; detach it so the handler only
        // tracks its own principal, like a real request (the FK is Restrict, and a tracked
        // dependent would drag Remove into EF's cascade path).
        db.ChangeTracker.Clear();

        await new DeleteBatchHandler(db, user)
            .Handle(new DeleteBatchCommand(batchId), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the links stay with the batch.
        var stored = await db.Batches.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.Batches.ToArrayAsync());
        Assert.Equal(1, await db.BatchProducts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteBatchHandler(db, user)
                .Handle(new DeleteBatchCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFoundAndKeepsTheRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedBatchAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteBatchHandler(db, user)
                .Handle(new DeleteBatchCommand(foreignId), CancellationToken.None));

        var stored = await db.Batches.IgnoreQueryFilters().SingleAsync();
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_CanBeRecreatedWithTheSameName()
    {
        // The filtered unique index (CompanyId, Name) WHERE IsDeleted = 0 frees the slot.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA, name: "Santan Batch Pertama");

        await new DeleteBatchHandler(db, user)
            .Handle(new DeleteBatchCommand(batchId), CancellationToken.None);
        var recreated = await SeedBatchAsync(db, CompanyA, name: "Santan Batch Pertama");

        Assert.Equal(1, await db.Batches.CountAsync());
        Assert.Equal(recreated, (await db.Batches.SingleAsync()).Id);
    }
}
