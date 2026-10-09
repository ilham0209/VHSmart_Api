using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Tests.Features.Product.ManageMenu;
using VHSmart_Api.Tests.Features.Product.ManageMenuConcept;
using VHSmart_Api.Tests.Features.Product.ManageProduct;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Menu Information (Current) / (Approved) tabs (spec 7.7 [CONFIRMED]): the premise's
// MenuConcept's ACTIVE menu links x each menu's raw materials with the material's HALAL
// CERTIFICATE columns; Approved is the same rows only while the premise has a certificate
// item from an approval (Database.md 12), otherwise "empty if none".
public class PremiseMenuInformationTests
{
    private static async Task<(Guid PremiseId, Guid ConceptId, Guid BrandId)>
        SeedPremiseWithConceptAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var conceptId = await MenuConceptTestData.SeedConceptAsync(db, companyId: companyId);
        var brandId = await PremiseTestData.SeedBrandAsync(db, companyId, "Seri Rasa");
        var premiseId = await PremiseTestData.SeedPremiseAsync(
            db, companyId, name: "Rosa Cafe", premiseType: PremiseType.RestaurantsAndCafe);
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        premise.MenuConceptId = conceptId;
        premise.BrandId = brandId;
        await db.SaveChangesAsync();
        return (premiseId, conceptId, brandId);
    }

    private static async Task<(Guid MenuId, Guid RawMaterialId)> SeedMenuWithMaterialAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string menuName = "Nasi Lemak",
        string ingredient = "Rice Flour",
        bool withCertificate = true)
    {
        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: ingredient, companyId: companyId);
        var menuId = await MenuTestData.SeedMenuAsync(
            db, name: menuName, companyId: companyId, rawMaterialIds: [rawMaterialId]);
        if (withCertificate)
        {
            await ProductTestData.SeedHalalCertificateAsync(
                db,
                rawMaterialId,
                expiryDate: new DateTime(2027, 6, 30),
                referenceNo: "JAKIM/1/2026/0001",
                authority: "JAKIM",
                companyId: companyId);
        }
        return (menuId, rawMaterialId);
    }

    private static async Task<IReadOnlyList<PremiseMenuInformationRow>> QueryAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid premiseId,
        bool approved = false) =>
        await new GetPremiseMenuInformationHandler(db, user)
            .Handle(
                new GetPremiseMenuInformationQuery(premiseId, approved),
                CancellationToken.None);

    [Fact]
    public async Task Handle_PremiseWithLinkedMenu_ReturnsIngredientRowWithCertificate()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var (menuId, rawMaterialId) = await SeedMenuWithMaterialAsync(db, user.CompanyId);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);

        var rows = await QueryAsync(db, user, premiseId);

        var row = Assert.Single(rows);
        Assert.Equal(menuId, row.MenuId);
        Assert.Equal("Nasi Lemak", row.MenuName);
        Assert.Equal("Seri Rasa", row.Brand);
        Assert.Equal(rawMaterialId, row.RawMaterialId);
        Assert.Equal("Rice Flour", row.Ingredient);
        Assert.Equal("Santan Foods", row.ManufacturerName);
        Assert.Equal("JAKIM/1/2026/0001", row.ReferenceNo);
        Assert.Equal("JAKIM", row.Authority);
        Assert.Equal(new DateOnly(2027, 6, 30), row.ExpiryDate);
        Assert.Equal(HalalStatus.Valid, row.Status);
    }

    [Fact]
    public async Task Handle_InactiveMenuLink_IsNotListed()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var (menuId, _) = await SeedMenuWithMaterialAsync(db, user.CompanyId);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId,
            companyId: user.CompanyId,
            mappingStatus: MenuConceptMenuMappingStatus.Inactive);

        Assert.Empty(await QueryAsync(db, user, premiseId));
    }

    [Fact]
    public async Task Handle_PremiseWithoutMenuConcept_ReturnsEmpty()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var conceptId = await MenuConceptTestData.SeedConceptAsync(db, companyId: user.CompanyId);
        var (menuId, _) = await SeedMenuWithMaterialAsync(db, user.CompanyId);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);

        Assert.Empty(await QueryAsync(db, user, premiseId));
    }

    [Fact]
    public async Task Handle_MenuWithoutMaterials_ReturnsRowWithEmptyIngredientCells()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var menuId = await MenuTestData.SeedMenuAsync(
            db, name: "Plain Rice", companyId: user.CompanyId, rawMaterialIds: []);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);

        var row = Assert.Single(await QueryAsync(db, user, premiseId));
        Assert.Equal(menuId, row.MenuId);
        Assert.Equal("Plain Rice", row.MenuName);
        Assert.Null(row.RawMaterialId);
        Assert.Null(row.Ingredient);
        Assert.Null(row.Status);
    }

    [Fact]
    public async Task Handle_MaterialWithoutCertificate_LeavesCertificateColumnsNull()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var (menuId, _) = await SeedMenuWithMaterialAsync(
            db, user.CompanyId, ingredient: "Water", withCertificate: false);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);

        var row = Assert.Single(await QueryAsync(db, user, premiseId));
        Assert.Equal("Water", row.Ingredient);
        Assert.Equal("Santan Foods", row.ManufacturerName);
        Assert.Null(row.ReferenceNo);
        Assert.Null(row.Authority);
        Assert.Null(row.ExpiryDate);
        Assert.Null(row.Status);
    }

    [Fact]
    public async Task Handle_ApprovedWithoutCertificateItem_ReturnsEmpty()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var (menuId, _) = await SeedMenuWithMaterialAsync(db, user.CompanyId);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);

        Assert.NotEmpty(await QueryAsync(db, user, premiseId));
        Assert.Empty(await QueryAsync(db, user, premiseId, approved: true));
    }

    [Fact]
    public async Task Handle_ApprovedWithPremiseCertificateItem_ReturnsTheMenuRows()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, conceptId, _) = await SeedPremiseWithConceptAsync(db, user.CompanyId);
        var (menuId, rawMaterialId) = await SeedMenuWithMaterialAsync(db, user.CompanyId);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: user.CompanyId);
        var (applicationId, _) = await CertificateTestData
            .SeedSubmittedApplicationWithBatchAsync(db, user.CompanyId);
        await CertificateTestData.SeedCertificateItemAsync(
            db, user.CompanyId, applicationId,
            itemName: "Rosa Cafe", premiseId: premiseId);

        var rows = await QueryAsync(db, user, premiseId, approved: true);

        var row = Assert.Single(rows);
        Assert.Equal(menuId, row.MenuId);
        Assert.Equal(rawMaterialId, row.RawMaterialId);
        Assert.Equal(HalalStatus.Valid, row.Status);
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
