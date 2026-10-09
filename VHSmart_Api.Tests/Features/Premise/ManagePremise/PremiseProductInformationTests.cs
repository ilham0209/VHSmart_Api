using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Tests.Features.Product.ManageProduct;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Product Information (Current) / (Approved) tabs (spec 7.7 [CONFIRMED]): Current = the
// company's products x their ACTIVE ingredient links with the material's HALAL CERTIFICATE
// columns; Approved = one row per AppCertificateItems row with a ProductId - the live
// product's data while it exists, the stored snapshot after a soft delete (Database.md 12).
public class PremiseProductInformationTests
{
    private static async Task<(Guid ProductId, Guid RawMaterialId)> SeedProductWithIngredientAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string productName,
        string ingredient,
        string mappingStatus = ProductIngredientMappingStatus.Active,
        bool withCertificate = true,
        DateTime? certificateExpiry = null)
    {
        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: ingredient, companyId: companyId);
        var productId = await ProductTestData.SeedProductAsync(
            db, name: productName, companyId: companyId);
        await ProductTestData.SeedIngredientAsync(
            db, productId, rawMaterialId,
            mappingStatus: mappingStatus, companyId: companyId);
        if (withCertificate)
        {
            await ProductTestData.SeedHalalCertificateAsync(
                db,
                rawMaterialId,
                expiryDate: certificateExpiry ?? new DateTime(2027, 6, 30),
                referenceNo: "JAKIM/1/2026/0002",
                authority: "JAKIM",
                companyId: companyId);
        }
        return (productId, rawMaterialId);
    }

    private static async Task<IReadOnlyList<PremiseProductInformationRow>> QueryAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid premiseId,
        bool approved = false) =>
        await new GetPremiseProductInformationHandler(db, user)
            .Handle(
                new GetPremiseProductInformationQuery(premiseId, approved),
                CancellationToken.None);

    [Fact]
    public async Task Handle_CurrentProducts_ReturnRowsWithActiveIngredientsOnly()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var (kicapId, soyId) = await SeedProductWithIngredientAsync(
            db, user.CompanyId, "Kicap Manis", "Soy Sauce",
            certificateExpiry: new DateTime(2020, 1, 1));
        var saltId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Salt", companyId: user.CompanyId);
        await ProductTestData.SeedIngredientAsync(
            db, kicapId, saltId,
            mappingStatus: ProductIngredientMappingStatus.Inactive, companyId: user.CompanyId);
        await ProductTestData.SeedProductAsync(db, name: "Plain Product", companyId: user.CompanyId);
        await ProductTestData.SeedProductAsync(db, name: "Foreign Product", companyId: otherCompany.CompanyId);

        var rows = await QueryAsync(db, user, premiseId);

        var kicapRow = Assert.Single(rows, row => row.Ingredient == "Soy Sauce");
        Assert.Equal(kicapId, kicapRow.ProductId);
        Assert.Equal("Kicap Manis", kicapRow.ProductName);
        Assert.Equal(soyId, kicapRow.RawMaterialId);
        Assert.Equal("Santan Foods", kicapRow.ManufacturerName);
        Assert.Equal("JAKIM/1/2026/0002", kicapRow.ReferenceNo);
        // D-04 over the product path: a 2020 expiry is Expired and shows its date.
        Assert.Equal(HalalStatus.Expired, kicapRow.Status);
        Assert.Equal(new DateOnly(2020, 1, 1), kicapRow.ExpiryDate);

        // The INACTIVE link's ingredient never renders; the unlinked product still gets its
        // own row with empty ingredient cells; another company's product is not ours to show.
        Assert.DoesNotContain(rows, row => row.Ingredient == "Salt");
        var plainRow = Assert.Single(rows, row => row.ProductName == "Plain Product");
        Assert.Null(plainRow.RawMaterialId);
        Assert.Null(plainRow.Ingredient);
        Assert.DoesNotContain(rows, row => row.ProductName == "Foreign Product");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Handle_ApprovedWithoutCertificateItems_ReturnsEmpty()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        await SeedProductWithIngredientAsync(db, user.CompanyId, "Kicap Manis", "Soy Sauce");

        Assert.NotEmpty(await QueryAsync(db, user, premiseId));
        Assert.Empty(await QueryAsync(db, user, premiseId, approved: true));
    }

    [Fact]
    public async Task Handle_ApprovedItemWithLiveProduct_ShowsTheLiveData()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var (productId, _) = await SeedProductWithIngredientAsync(
            db, user.CompanyId, "Live Product Name", "Soy Sauce");
        var (applicationId, _) = await CertificateTestData
            .SeedSubmittedApplicationWithBatchAsync(db, user.CompanyId);
        await CertificateTestData.SeedCertificateItemAsync(
            db, user.CompanyId, applicationId,
            itemName: "Approved Snapshot Name", productId: productId);

        var rows = await QueryAsync(db, user, premiseId, approved: true);

        // The live product's name wins over the snapshot while it exists, ingredient rows
        // and all.
        var row = Assert.Single(rows);
        Assert.Equal(productId, row.ProductId);
        Assert.Equal("Live Product Name", row.ProductName);
        Assert.Equal("Soy Sauce", row.Ingredient);
        Assert.Equal(HalalStatus.Valid, row.Status);
    }

    [Fact]
    public async Task Handle_ApprovedItemForSoftDeletedProduct_ShowsTheSnapshotOnly()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var (productId, _) = await SeedProductWithIngredientAsync(
            db, user.CompanyId, "Doomed Product", "Soy Sauce");
        var (applicationId, _) = await CertificateTestData
            .SeedSubmittedApplicationWithBatchAsync(db, user.CompanyId);
        await CertificateTestData.SeedCertificateItemAsync(
            db, user.CompanyId, applicationId,
            itemName: "Approved Snapshot Name", productId: productId);
        var product = await db.Products.SingleAsync(row => row.Id == productId);
        product.IsDeleted = true;
        await db.SaveChangesAsync();

        var rows = await QueryAsync(db, user, premiseId, approved: true);

        // Soft delete: the certificate item keeps its snapshot, the ingredient rows of the
        // deleted product do not come back through the links.
        var row = Assert.Single(rows);
        Assert.Equal(productId, row.ProductId);
        Assert.Equal("Approved Snapshot Name", row.ProductName);
        Assert.Null(row.RawMaterialId);
        Assert.Null(row.Ingredient);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignPremiseId = await PremiseTestData.SeedPremiseAsync(
            db, otherCompany.CompanyId, "Foreign premise");

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            QueryAsync(db, user, Guid.NewGuid()));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            QueryAsync(db, user, foreignPremiseId));

        Assert.Equal("Premise not found.", unknown.Message);
        Assert.Equal("Premise not found.", foreign.Message);
    }
}
