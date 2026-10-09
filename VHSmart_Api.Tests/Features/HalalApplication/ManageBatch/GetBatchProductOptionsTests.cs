using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

public class GetBatchProductOptionsTests
{
    [Fact]
    public async Task Handle_Picker_ExcludesProductsOfThisBatchAndOfOtherCompanies()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var linkedId = await SeedValidProductAsync(db, CompanyA, name: "Sudah Linked");
        await SeedBatchProductAsync(db, CompanyA, batchId, linkedId);
        await SeedValidProductAsync(db, CompanyA, name: "Masih Tersedia");
        await SeedValidProductAsync(db, CompanyB, name: "Foreign Product");

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(new GetBatchProductOptionsQuery(batchId), CancellationToken.None);

        var option = Assert.Single(response.Data);
        Assert.Equal("Masih Tersedia", option.Name);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_InactivePair_IsNotOfferedInThePicker()
    {
        // The INACTIVE pair sits on the modal's own list with its link icon instead.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var inactiveId = await SeedValidProductAsync(db, CompanyA, name: "Sudah Pernah");
        await SeedBatchProductAsync(
            db, CompanyA, batchId, inactiveId, BatchProductMappingStatus.Inactive);
        await SeedValidProductAsync(db, CompanyA, name: "Masih Tersedia");

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(new GetBatchProductOptionsQuery(batchId), CancellationToken.None);

        var option = Assert.Single(response.Data);
        Assert.Equal("Masih Tersedia", option.Name);
    }

    [Fact]
    public async Task Handle_DerivedColumns_AreFilledAfterPaging()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        await SeedValidProductAsync(db, CompanyA, name: "Santan Kicap");
        await SeedUnlinkedProductAsync(db, CompanyA, name: "Santan Tepung");
        await SeedExpiredProductAsync(db, CompanyA, name: "Santan Tengo");

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(new GetBatchProductOptionsQuery(batchId), CancellationToken.None);

        var valid = response.Data.Single(row => row.Name == "Santan Kicap");
        Assert.Equal("Sereni", valid.Brand);
        Assert.Equal(ProductIngredientLinkStatus.Linked, valid.LinkIngredientStatus);
        Assert.Equal(HalalStatus.Valid, valid.HalalStatus);

        var unlinked = response.Data.Single(row => row.Name == "Santan Tepung");
        Assert.Equal(ProductIngredientLinkStatus.Unlinked, unlinked.LinkIngredientStatus);
        Assert.Equal(HalalStatus.Expired, unlinked.HalalStatus);

        var expired = response.Data.Single(row => row.Name == "Santan Tengo");
        Assert.Equal(ProductIngredientLinkStatus.Linked, expired.LinkIngredientStatus);
        Assert.Equal(HalalStatus.Expired, expired.HalalStatus);
    }

    [Fact]
    public async Task Handle_SearchOnNameOrCodeOrGtin_FiltersRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        await SeedValidProductAsync(db, CompanyA, name: "Santan Kicap");
        await SeedValidProductAsync(db, CompanyA, name: "Kicap Pekat");

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(
                new GetBatchProductOptionsQuery(batchId)
                {
                    Request = { SearchTerm = "santan" }
                },
                CancellationToken.None);

        var option = Assert.Single(response.Data);
        Assert.Equal("Santan Kicap", option.Name);
        Assert.Equal(1, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_DefaultOrder_IsNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);
        await SeedValidProductAsync(db, CompanyA, name: "Zulu Product");
        await SeedValidProductAsync(db, CompanyA, name: "Alpha Product");

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(new GetBatchProductOptionsQuery(batchId), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Product", "Zulu Product" },
            response.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_NoMatchOnTheDerivedColumns_ReturnsAnEmptyPage()
    {
        // The derived statuses never take part in the search, so a miss is a plain empty page
        // - the post-paging fill must not blow up on it.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var batchId = await SeedBatchAsync(db, CompanyA);

        var response = await new GetBatchProductOptionsHandler(db, user)
            .Handle(
                new GetBatchProductOptionsQuery(batchId)
                {
                    Request = { SearchTerm = "tidak-ada" }
                },
                CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(0, response.TotalRecords);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchProductOptionsHandler(db, user)
                .Handle(
                    new GetBatchProductOptionsQuery(Guid.NewGuid()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BatchOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignBatch = await SeedBatchAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBatchProductOptionsHandler(db, user)
                .Handle(
                    new GetBatchProductOptionsQuery(foreignBatch),
                    CancellationToken.None));
    }
}
