using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
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
}
