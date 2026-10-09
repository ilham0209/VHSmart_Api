using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class GetBatchByIdTests
{
    [Fact]
    public async Task Handle_ProductSchemeBatch_ReturnsTheProductList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA, name: "Batch Product Halal A");
        var validId = await SeedValidProductAsync(db, CompanyA, name: "Santan Kicap");
        var unlinkedId = await SeedUnlinkedProductAsync(db, CompanyA, name: "Santan Tepung");
        await SeedBatchProductAsync(db, CompanyA, batchId, validId);
        await SeedBatchProductAsync(
            db, CompanyA, batchId, unlinkedId, BatchProductMappingStatus.Inactive);

        var response = await new GetBatchByIdHandler(db, user)
            .Handle(new GetBatchByIdQuery(batchId), CancellationToken.None);

        Assert.Equal(batchId, response.Id);
        Assert.Equal("Batch Product Halal A", response.Name);
        Assert.False(response.IsFoodPremiseScheme);
        Assert.Empty(response.Premises);

        Assert.Equal(2, response.Products.Count);
        var valid = response.Products.Single(row => row.ProductId == validId);
        Assert.Equal("Santan Kicap", valid.ProductName);
        Assert.Equal("Sereni", valid.Brand);
        Assert.Equal("Linked", valid.LinkIngredientStatus);
        Assert.Equal(BatchProductMappingStatus.Active, valid.MappingStatus);

        var unlinked = response.Products.Single(row => row.ProductId == unlinkedId);
        Assert.Equal("Unlinked", unlinked.LinkIngredientStatus);
        Assert.Equal(BatchProductMappingStatus.Inactive, unlinked.MappingStatus);
    }

    [Fact]
    public async Task Handle_FoodPremiseBatch_ReturnsItsPremisesOnly()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, name: "Batch 271125/2", schemeId: await FoodPremiseSchemeIdAsync(db));
        var completeId = await SeedCompletePremiseAsync(db, CompanyA, name: "PREMISE C");
        await SeedBatchPremiseAsync(db, CompanyA, batchId, completeId);

        var response = await new GetBatchByIdHandler(db, user)
            .Handle(new GetBatchByIdQuery(batchId), CancellationToken.None);

        Assert.True(response.IsFoodPremiseScheme);
        Assert.Empty(response.Products);
        var premise = Assert.Single(response.Premises);
        Assert.Equal(completeId, premise.PremiseId);
        Assert.Equal("PREMISE C", premise.PremiseName);
    }

    [Fact]
    public async Task Handle_PremiseDeletedAfterLink_DropsOutOfTheList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(
            db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);
        await SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);

        // The fixture seeded the attachments through this context; detach everything so the
        // premise removal only tracks the principal (a real request never loads the children).
        db.ChangeTracker.Clear();
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        db.Premises.Remove(premise);
        await db.SaveChangesAsync();

        var response = await new GetBatchByIdHandler(db, user)
            .Handle(new GetBatchByIdQuery(batchId), CancellationToken.None);

        Assert.Empty(response.Premises);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchByIdHandler(db, user)
                .Handle(new GetBatchByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedBatchAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchByIdHandler(db, user)
                .Handle(new GetBatchByIdQuery(foreignId), CancellationToken.None));
    }
}
