using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.ManageProduct.ProductTestData;

namespace VHSmart_Api.Tests.Features.Product.ManageProduct;

public class GetProductIngredientsTests
{
    // Spec 6.3 / 9.1, verbatim.
    private const string BrandRequiredMessage =
        "Please assign at least one Brand (Manage Brand Information) to retrieve the Ingredient Information";

    [Fact]
    public async Task Handle_LinkedRowWithCertificate_ReturnsTheSpecColumns()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db, "Rice Flour");
        await SeedHalalCertificateAsync(
            db, rawMaterialId, new DateTime(2030, 6, 30), "JAKIM/1/0001", "JAKIM");
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        var row = Assert.Single(result);
        Assert.Equal("Rice Flour", row.Ingredient);
        Assert.Equal(ProductIngredientMappingStatus.Active, row.MappingStatus);
        Assert.Equal("Santan Foods", row.ManufacturerInformation.Name);
        Assert.Equal("Jalan Gombak 1", row.ManufacturerInformation.Address);
        Assert.Equal("0312345678", row.ManufacturerInformation.Contact);
        Assert.Equal("JAKIM/1/0001", row.HalalCertificateInformation.ReferenceNo);
        Assert.Equal("JAKIM", row.HalalCertificateInformation.Authority);
        Assert.Equal(new DateOnly(2030, 6, 30), row.HalalCertificateInformation.ExpiryDate);
        Assert.Equal(HalalStatus.Valid, row.HalalCertificateInformation.Status);
    }

    [Fact]
    public async Task Handle_MaterialWithoutCertificate_AnswersNullStatus()
    {
        // The spec renders that cell as "Not Available"; the API answers null so the client
        // never has to tell "expired on an unknown date" from "no certificate at all".
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        var certificate = Assert.Single(result).HalalCertificateInformation;
        Assert.Null(certificate.ReferenceNo);
        Assert.Null(certificate.Authority);
        Assert.Null(certificate.ExpiryDate);
        Assert.Null(certificate.Status);
    }

    [Fact]
    public async Task Handle_ExpiredCertificate_AnswersExpired()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedHalalCertificateAsync(db, rawMaterialId, new DateTime(2020, 1, 1));
        await SeedIngredientAsync(db, productId, rawMaterialId);

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        Assert.Equal(HalalStatus.Expired, Assert.Single(result).HalalCertificateInformation.Status);
    }

    [Fact]
    public async Task Handle_UnlinkedRow_IsStillListedWithItsStatus()
    {
        // Database.md 9: unlink toggles MappingStatus, it does not remove the row - the table
        // shows the status and the Action icon links it again.
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var rawMaterialId = await SeedRawMaterialAsync(db);
        await SeedIngredientAsync(
            db, productId, rawMaterialId, ProductIngredientMappingStatus.Inactive);

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        Assert.Equal(
            ProductIngredientMappingStatus.Inactive,
            Assert.Single(result).MappingStatus);
    }

    [Fact]
    public async Task Handle_SeveralRows_AreOrderedByIngredientName()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        await SeedIngredientAsync(db, productId, await SeedRawMaterialAsync(db, "Zebra Starch"));
        await SeedIngredientAsync(db, productId, await SeedRawMaterialAsync(db, "Alpha Starch"));

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Starch", "Zebra Starch" },
            result.Select(row => row.Ingredient).ToArray());
    }

    [Fact]
    public async Task Handle_CompanyWithoutBrandLink_ThrowsBusinessRule()
    {
        var db = await CreateDbAsync(UserA());
        var productId = await SeedProductAsync(db);
        await SeedRawMaterialAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new GetProductIngredientsHandler(db, UserA())
                .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None));

        Assert.Equal(BrandRequiredMessage, exception.Message);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductIngredientsHandler(db, UserA())
                .Handle(new GetProductIngredientsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ProductOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserB());
        await SeedProductAsync(db, companyId: CompanyA);
        var foreignId = await db.Products.IgnoreQueryFilters()
            .Select(row => row.Id)
            .SingleAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductIngredientsHandler(db, UserB())
                .Handle(new GetProductIngredientsQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RawMaterialOfAnotherCompany_IsNotListed()
    {
        // The raw material of a company that never shared it is invisible under the 7.3
        // filter, so the link has nothing to render and the row drops out of the table.
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var foreignMaterialId = await SeedRawMaterialAsync(db, "Foreign Sugar", companyId: CompanyB);
        await SeedIngredientAsync(db, productId, foreignMaterialId);

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_SoftDeletedLink_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        await SeedCompanyBrandAsync(db);
        var productId = await SeedProductAsync(db);
        var ingredientId = await SeedIngredientAsync(
            db, productId, await SeedRawMaterialAsync(db));
        var ingredient = await db.ProductIngredients.SingleAsync(row => row.Id == ingredientId);
        db.ProductIngredients.Remove(ingredient);
        await db.SaveChangesAsync();

        var result = await new GetProductIngredientsHandler(db, UserA())
            .Handle(new GetProductIngredientsQuery(productId), CancellationToken.None);

        Assert.Empty(result);
    }
}
