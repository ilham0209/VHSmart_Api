using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class LinkBatchPremiseTests
{
    private static async Task<Guid> FoodPremiseBatchAsync(TestableVHSmartDbContext db) =>
        await SeedBatchAsync(db, CompanyA, schemeId: await FoodPremiseSchemeIdAsync(db));

    [Fact]
    public async Task Handle_CompletePremise_InsertsARowAndAnswersTheListRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA, name: "PREMISE C");

        var response = await new LinkBatchPremiseHandler(db, user)
            .Handle(new LinkBatchPremiseCommand(batchId, premiseId), CancellationToken.None);

        var stored = await db.BatchPremises.SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(stored.Id, response.BatchPremiseId);
        Assert.Equal(premiseId, response.PremiseId);
        Assert.Equal("PREMISE C", response.PremiseName);
    }

    // D-15: the Associate Premise pick-list offers only complete premises; the API holds a
    // direct call to the same rule instead of trusting the client's filter.
    [Fact]
    public async Task Handle_IncompletePremise_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        var premiseId = await SeedIncompletePremiseAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchPremiseHandler(db, user)
                .Handle(new LinkBatchPremiseCommand(batchId, premiseId), CancellationToken.None));

        Assert.Equal(
            "Only premises with complete documentation can be added to a batch.",
            exception.Message);
        Assert.Empty(await db.BatchPremises.ToArrayAsync());
    }

    [Fact]
    public async Task Handle_ProductSchemeBatch_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new LinkBatchPremiseHandler(db, user)
                .Handle(new LinkBatchPremiseCommand(batchId, premiseId), CancellationToken.None));

        Assert.Equal("This batch does not accept premises.", exception.Message);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedPremise_DoesNotInsertASecondRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);
        var existingId = await SeedBatchPremiseAsync(db, CompanyA, batchId, premiseId);

        var response = await new LinkBatchPremiseHandler(db, user)
            .Handle(new LinkBatchPremiseCommand(batchId, premiseId), CancellationToken.None);

        var stored = await db.BatchPremises.SingleAsync();
        Assert.Equal(existingId, stored.Id);
        Assert.Equal(existingId, response.BatchPremiseId);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var premiseId = await SeedCompletePremiseAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkBatchPremiseHandler(db, user)
                .Handle(new LinkBatchPremiseCommand(Guid.NewGuid(), premiseId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PremiseOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);
        var foreignPremiseId = await SeedCompletePremiseAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkBatchPremiseHandler(db, user)
                .Handle(new LinkBatchPremiseCommand(batchId, foreignPremiseId), CancellationToken.None));

        Assert.Empty(await db.BatchPremises.ToArrayAsync());
    }

    [Fact]
    public async Task Handle_UnknownPremise_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await FoodPremiseBatchAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LinkBatchPremiseHandler(db, user)
                .Handle(new LinkBatchPremiseCommand(batchId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingPremiseId_Fails()
    {
        var validator = new LinkBatchPremiseValidator();

        var result = await validator.ValidateAsync(
            new LinkBatchPremiseCommand(Guid.NewGuid(), Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Premise is required.");
    }
}
