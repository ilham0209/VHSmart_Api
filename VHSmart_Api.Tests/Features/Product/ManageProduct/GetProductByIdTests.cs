using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class GetProductByIdTests
{
    [Fact]
    public async Task Handle_ExistingRow_ReturnsEveryFormField()
    {
        var db = await CreateDbAsync(UserA());
        var marketingMethodId = await SeedMarketingMethodAsync(db);
        var id = await SeedProductAsync(db);
        var row = await db.Products.SingleAsync();
        row.MarketingMethodId = marketingMethodId;
        row.Gtin = "9551234567890";
        row.NutritionContentClaims = "No MSG";
        row.PotentialAllergens = "None";
        row.CalorieContent = "120 kcal";
        row.AvailableAt = "Malaysia";
        row.PackagingSize = "250 ml";
        await db.SaveChangesAsync();

        var response = await new GetProductByIdHandler(db, UserA())
            .Handle(new GetProductByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal("Santan Kicap", response.Name);
        Assert.Equal("Santan Foods", response.ManufacturerName);
        Assert.Equal("PRD-001", response.Code);
        Assert.Equal("9551234567890", response.Gtin);
        Assert.Equal(marketingMethodId, response.MarketingMethodId);
        Assert.Null(response.QrCodeKey);
    }

    [Fact]
    public async Task Handle_SupplierOnlyManufacturerRow_AnswersTheSupplierName()
    {
        var db = await CreateDbAsync(UserA());
        var supplierId = await SeedSupplierOnlyAsync(db);
        var id = await SeedProductAsync(db, manufacturerId: supplierId);

        var response = await new GetProductByIdHandler(db, UserA())
            .Handle(new GetProductByIdQuery(id), CancellationToken.None);

        Assert.Equal("Santan Supplies", response.ManufacturerName);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductByIdHandler(db, UserA())
                .Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductByIdHandler(db, UserA())
                .Handle(new GetProductByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ViewAllToken_StillOnlyReadsOwnRows()
    {
        // The premise stance (PR-01): the explicit CompanyId match keeps a Switch Company =
        // ALL caller on their own rows for detail, update and delete alike.
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        var foreignId = await SeedProductAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductByIdHandler(db, viewAll)
                .Handle(new GetProductByIdQuery(foreignId), CancellationToken.None));
    }
}
