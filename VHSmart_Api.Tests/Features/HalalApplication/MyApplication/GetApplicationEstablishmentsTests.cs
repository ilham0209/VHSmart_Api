using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetApplicationEstablishmentsTests
{
    [Fact]
    public async Task Handle_LinkedPremises_AreReturnedWithTheirDocumentStatus()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var completeId = await BatchTestData.SeedCompletePremiseAsync(
            db, CompanyA, name: "Seri Rasa Factory");
        var incompleteId = await BatchTestData.SeedIncompletePremiseAsync(
            db, CompanyA, name: "Kedai Kopi Dummy");
        await BatchTestData.SeedBatchPremiseAsync(db, CompanyA, batchId, completeId);
        await BatchTestData.SeedBatchPremiseAsync(db, CompanyA, batchId, incompleteId);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationEstablishmentsHandler(db, user)
            .Handle(
                new GetApplicationEstablishmentsQuery(applicationId),
                CancellationToken.None);

        Assert.Equal(2, rows.Count);
        var complete = rows.Single(row => row.PremiseId == completeId);
        Assert.Equal("Seri Rasa Factory", complete.EstablishmentName);
        Assert.Equal(
            PremiseDocumentStatusCalculator.CompleteText, complete.DocumentStatus);
        Assert.False(string.IsNullOrWhiteSpace(complete.Address));
        Assert.False(string.IsNullOrWhiteSpace(complete.Telephone));

        var incomplete = rows.Single(row => row.PremiseId == incompleteId);
        Assert.Equal(
            PremiseDocumentStatusCalculator.NotCompleteText,
            incomplete.DocumentStatus);
    }

    [Fact]
    public async Task Handle_UnlinkedPremise_IsNotReturned()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        await BatchTestData.SeedCompletePremiseAsync(db, CompanyA, name: "Not Linked");
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var rows = await new GetApplicationEstablishmentsHandler(db, user)
            .Handle(
                new GetApplicationEstablishmentsQuery(applicationId),
                CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ApplicationWithoutBatch_ReturnsEmpty()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var rows = await new GetApplicationEstablishmentsHandler(db, user)
            .Handle(
                new GetApplicationEstablishmentsQuery(applicationId),
                CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetApplicationEstablishmentsHandler(db, user)
                .Handle(
                    new GetApplicationEstablishmentsQuery(foreignId),
                    CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }
}
