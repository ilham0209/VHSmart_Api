using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class GetAllRawMaterialsTests
{
    [Fact]
    public async Task Handle_ReturnsTheSpecColumns()
    {
        var db = await CreateDbAsync(UserA());
        var manufacturerId = await SeedManufacturerAsync(db);
        var partner = await SeedCompanyAsync(db, "Sharing Partner");
        var statusId = await SeedGeneralDataAsync(db, "Ingredient Status", "Active");
        await SeedRowAsync(
            db,
            ingredient: "Rice Flour",
            ingredientCode: "RM-001",
            ingredientStatusId: statusId,
            manufacturerId: manufacturerId,
            accessibleCompanyIds: [partner],
            isPackagingMaterial: true);

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal("RM-001", row.IngredientCode);
        Assert.Equal("Rice Flour", row.Ingredient);
        Assert.Equal("Santan Foods", row.ManufacturerName);
        Assert.Equal("Active", row.IngredientStatus);
        Assert.Equal("Sharing Partner", Assert.Single(row.AccessibleFor));
        Assert.True(row.IsPackagingMaterial);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsIngredientOrCode()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db, ingredient: "Rice Flour", ingredientCode: "RM-001");
        await SeedRowAsync(db, ingredient: "Corn Flour", ingredientCode: "RM-002");

        var byIngredient = await new GetAllRawMaterialsHandler(db).Handle(
            new GetAllRawMaterialsQuery
            {
                Request = new DataGridRequest { SearchTerm = "corn" }
            },
            CancellationToken.None);
        var byCode = await new GetAllRawMaterialsHandler(db).Handle(
            new GetAllRawMaterialsQuery
            {
                Request = new DataGridRequest { SearchTerm = "RM-001" }
            },
            CancellationToken.None);

        Assert.Equal("Corn Flour", Assert.Single(byIngredient.Data).Ingredient);
        Assert.Equal("Rice Flour", Assert.Single(byCode.Data).Ingredient);
    }

    [Fact]
    public async Task Handle_OwnCompanyRow_IsVisible()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db);

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_RowSharedToTheCaller_IsVisible()
    {
        var db = await CreateDbAsync(UserB());
        await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        // CodingRules 7.3: owner OR listed in "Accessible For".
        Assert.Equal(1, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_UnsharedRowOfAnotherCompany_IsInvisible()
    {
        var db = await CreateDbAsync(UserB());
        await SeedRowAsync(db, companyId: CompanyA);

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_ViewAllToken_SeesEveryRow()
    {
        var viewAll = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA, viewAllCompanies: true);
        var db = await CreateDbAsync(viewAll);
        await SeedRowAsync(db, companyId: CompanyA);
        await SeedRowAsync(db, ingredient: "Other Corn", ingredientCode: "RM-002", companyId: CompanyB);

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalRecords);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedRowAsync(db);
        db.RawMaterials.Remove(await db.RawMaterials.SingleAsync());
        await db.SaveChangesAsync();

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
        Assert.False(await db.RawMaterials.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == id && !row.IsDeleted));
    }

    [Fact]
    public async Task Handle_ClientSortByIngredient_Descending()
    {
        var db = await CreateDbAsync(UserA());
        await SeedRowAsync(db, ingredient: "Corn Flour", ingredientCode: "RM-002");
        await SeedRowAsync(db, ingredient: "Rice Flour", ingredientCode: "RM-001");

        var result = await new GetAllRawMaterialsHandler(db).Handle(
            new GetAllRawMaterialsQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = nameof(RawMaterialEntity.Ingredient),
                    SortDescending = true
                }
            },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Rice Flour", "Corn Flour" },
            result.Data.Select(row => row.Ingredient).ToArray());
    }

    [Fact]
    public async Task Handle_CertificateRow_ShowsTheHalalInformationColumn()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedRowAsync(db);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        await SeedAttachmentAsync(
            db, id, certificate,
            expiryDate: new DateTime(2099, 6, 1), referenceNo: "HC-001", authority: "JAKIM");

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        var info = Assert.Single(result.Data).HalalInformation;
        Assert.NotNull(info);
        Assert.Equal("HC-001", info.ReferenceNo);
        Assert.Equal("JAKIM", info.Authority);
        Assert.Equal(new DateOnly(2099, 6, 1), info.ExpiryDate);
        Assert.Equal(HalalStatus.Valid, info.Status);
    }

    [Fact]
    public async Task Handle_ExpiredCertificate_ReportsExpired()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedRowAsync(db);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        await SeedAttachmentAsync(db, id, certificate, expiryDate: new DateTime(2020, 1, 1));

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        var info = Assert.Single(result.Data).HalalInformation;
        Assert.NotNull(info);
        Assert.Equal(HalalStatus.Expired, info.Status);
        Assert.Equal(new DateOnly(2020, 1, 1), info.ExpiryDate);
    }

    [Fact]
    public async Task Handle_WithoutCertificate_OrWithAnotherType_LeavesTheColumnNull()
    {
        var db = await CreateDbAsync(UserA());
        var bareId = await SeedRowAsync(db);
        var otherTypeId = await SeedRowAsync(
            db, ingredient: "Corn Flour", ingredientCode: "RM-002");
        var processFlow = await SeedSupportingDocumentAsync(db, "PROCESS FLOW");
        await SeedAttachmentAsync(
            db, otherTypeId, processFlow, expiryDate: new DateTime(2020, 1, 1));

        var result = await new GetAllRawMaterialsHandler(db)
            .Handle(new GetAllRawMaterialsQuery(), CancellationToken.None);

        // Only a HALAL CERTIFICATE row fills the column - a process flow that happens to be
        // expired must not read as a halal problem, and a row with no uploads stays empty.
        Assert.Null(result.Data.Single(row => row.Id == bareId).HalalInformation);
        Assert.Null(result.Data.Single(row => row.Id == otherTypeId).HalalInformation);
    }
}
