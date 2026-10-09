using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.Product.ManageMenu;
using VHSmart_Api.Tests.Features.Product.ManageMenuConcept;
using VHSmart_Api.Tests.Features.Product.ManageProduct;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// "Download Halal Certificate" of the Menu / Product tab rows (spec 7.7): the raw
// material's stored HALAL CERTIFICATE file, streamed only for id pairs the tabs can render
// (premise scope -> concept link / product scope -> ACTIVE ingredient), 404 otherwise.
public class PremiseCertificateDownloadTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartPremiseTabCertTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public PremiseCertificateDownloadTests() =>
        _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private GetPremiseMenuCertificateHandler MenuHandler(
        TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage);

    private GetPremiseProductCertificateHandler ProductHandler(
        TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage);

    private async Task StoreCertificateBytesAsync(
        TestableVHSmartDbContext db, Guid rawMaterialId)
    {
        var attachment = await db.RawMaterialAttachments
            .AsNoTracking()
            .SingleAsync(row => row.RawMaterialId == rawMaterialId);
        Directory.CreateDirectory(_storageRoot);
        File.WriteAllBytes(
            Path.Combine(_storageRoot, attachment.Document.StorageKey),
            [37, 80, 68, 70]);
    }

    private static async Task<(Guid PremiseId, Guid MenuId, Guid RawMaterialId)>
        SeedMenuChainAsync(
            TestableVHSmartDbContext db,
            Guid companyId,
            bool withCertificate = true)
    {
        var conceptId = await MenuConceptTestData.SeedConceptAsync(db, companyId: companyId);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, companyId);
        var premise = await db.Premises.SingleAsync(row => row.Id == premiseId);
        premise.MenuConceptId = conceptId;
        await db.SaveChangesAsync();

        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Rice Flour", companyId: companyId);
        var menuId = await MenuTestData.SeedMenuAsync(
            db, companyId: companyId, rawMaterialIds: [rawMaterialId]);
        await MenuConceptTestData.SeedLinkAsync(
            db, conceptId, menuId, companyId: companyId);
        if (withCertificate)
        {
            await ProductTestData.SeedHalalCertificateAsync(
                db, rawMaterialId, companyId: companyId);
        }
        return (premiseId, menuId, rawMaterialId);
    }

    private static async Task<(Guid PremiseId, Guid ProductId, Guid RawMaterialId)>
        SeedProductChainAsync(
            TestableVHSmartDbContext db,
            Guid companyId,
            bool withCertificate = true,
            string mappingStatus = ProductIngredientMappingStatus.Active)
    {
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, companyId);
        var rawMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Soy Sauce", companyId: companyId);
        var productId = await ProductTestData.SeedProductAsync(
            db, name: "Kicap Manis", companyId: companyId);
        await ProductTestData.SeedIngredientAsync(
            db, productId, rawMaterialId,
            mappingStatus: mappingStatus, companyId: companyId);
        if (withCertificate)
        {
            await ProductTestData.SeedHalalCertificateAsync(
                db, rawMaterialId, companyId: companyId);
        }
        return (premiseId, productId, rawMaterialId);
    }

    [Fact]
    public async Task Handle_MenuCertificate_StreamsTheStoredFile()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, menuId, rawMaterialId) = await SeedMenuChainAsync(
            db, user.CompanyId);
        await StoreCertificateBytesAsync(db, rawMaterialId);

        var response = await MenuHandler(db, user).Handle(
            new GetPremiseMenuCertificateQuery(premiseId, menuId, rawMaterialId),
            CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Handle_MenuNotOnThePremisesConcept_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, _, rawMaterialId) = await SeedMenuChainAsync(db, user.CompanyId);
        var unlinkedMenuId = await MenuTestData.SeedMenuAsync(
            db, name: "Unlinked Menu", companyId: user.CompanyId, rawMaterialIds: []);

        var notOnPremise = await Assert.ThrowsAsync<NotFoundException>(() =>
            MenuHandler(db, user).Handle(
                new GetPremiseMenuCertificateQuery(premiseId, unlinkedMenuId, rawMaterialId),
                CancellationToken.None));

        Assert.Equal("Menu not found.", notOnPremise.Message);
    }

    [Fact]
    public async Task Handle_RawMaterialNotOnTheMenu_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, menuId, _) = await SeedMenuChainAsync(db, user.CompanyId);
        var otherMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Water", companyId: user.CompanyId);
        await ProductTestData.SeedHalalCertificateAsync(
            db, otherMaterialId, companyId: user.CompanyId);

        var notOnMenu = await Assert.ThrowsAsync<NotFoundException>(() =>
            MenuHandler(db, user).Handle(
                new GetPremiseMenuCertificateQuery(premiseId, menuId, otherMaterialId),
                CancellationToken.None));

        Assert.Equal("Ingredient not found.", notOnMenu.Message);
    }

    [Fact]
    public async Task Handle_MenuWithoutCertificate_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, menuId, rawMaterialId) = await SeedMenuChainAsync(
            db, user.CompanyId, withCertificate: false);

        var noDocument = await Assert.ThrowsAsync<NotFoundException>(() =>
            MenuHandler(db, user).Handle(
                new GetPremiseMenuCertificateQuery(premiseId, menuId, rawMaterialId),
                CancellationToken.None));

        Assert.Equal("No document.", noDocument.Message);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (_, menuId, rawMaterialId) = await SeedMenuChainAsync(db, user.CompanyId);
        var foreignPremiseId = await PremiseTestData.SeedPremiseAsync(
            db, otherCompany.CompanyId, "Foreign premise");

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            MenuHandler(db, user).Handle(
                new GetPremiseMenuCertificateQuery(Guid.NewGuid(), menuId, rawMaterialId),
                CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            MenuHandler(db, user).Handle(
                new GetPremiseMenuCertificateQuery(foreignPremiseId, menuId, rawMaterialId),
                CancellationToken.None));

        Assert.Equal("Premise not found.", unknown.Message);
        Assert.Equal("Premise not found.", foreign.Message);
    }

    [Fact]
    public async Task Handle_ProductCertificate_StreamsTheStoredFile()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, productId, rawMaterialId) = await SeedProductChainAsync(
            db, user.CompanyId);
        await StoreCertificateBytesAsync(db, rawMaterialId);

        var response = await ProductHandler(db, user).Handle(
            new GetPremiseProductCertificateQuery(premiseId, productId, rawMaterialId),
            CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Handle_UnknownOrForeignProduct_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var otherCompany = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, _, rawMaterialId) = await SeedProductChainAsync(db, user.CompanyId);
        var foreignProductId = await ProductTestData.SeedProductAsync(
            db, name: "Foreign Product", companyId: otherCompany.CompanyId);

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            ProductHandler(db, user).Handle(
                new GetPremiseProductCertificateQuery(premiseId, Guid.NewGuid(), rawMaterialId),
                CancellationToken.None));
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            ProductHandler(db, user).Handle(
                new GetPremiseProductCertificateQuery(premiseId, foreignProductId, rawMaterialId),
                CancellationToken.None));

        Assert.Equal("Product not found.", unknown.Message);
        Assert.Equal("Product not found.", foreign.Message);
    }

    [Fact]
    public async Task Handle_RawMaterialNotOnTheProduct_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, productId, _) = await SeedProductChainAsync(db, user.CompanyId);
        var otherMaterialId = await ProductTestData.SeedRawMaterialAsync(
            db, ingredient: "Water", companyId: user.CompanyId);
        await ProductTestData.SeedHalalCertificateAsync(
            db, otherMaterialId, companyId: user.CompanyId);

        var notOnProduct = await Assert.ThrowsAsync<NotFoundException>(() =>
            ProductHandler(db, user).Handle(
                new GetPremiseProductCertificateQuery(premiseId, productId, otherMaterialId),
                CancellationToken.None));

        Assert.Equal("Ingredient not found.", notOnProduct.Message);
    }

    [Fact]
    public async Task Handle_InactiveIngredientLink_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var (premiseId, productId, rawMaterialId) = await SeedProductChainAsync(
            db, user.CompanyId, mappingStatus: ProductIngredientMappingStatus.Inactive);
        await ProductTestData.SeedHalalCertificateAsync(
            db, rawMaterialId, companyId: user.CompanyId);

        var inactive = await Assert.ThrowsAsync<NotFoundException>(() =>
            ProductHandler(db, user).Handle(
                new GetPremiseProductCertificateQuery(premiseId, productId, rawMaterialId),
                CancellationToken.None));

        Assert.Equal("Ingredient not found.", inactive.Message);
    }
}
