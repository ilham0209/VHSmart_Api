using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.RawMaterial;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class GetRawMaterialOptionsTests
{
    [Fact]
    public async Task Handle_IngredientStatuses_AreOwnCompanyProductRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedGeneralDataAsync(db, "Ingredient Status", "Active");
        await SeedGeneralDataAsync(db, "Ingredient Status", "Inactive");
        // Another company's row and a row of the wrong category must not appear.
        await SeedGeneralDataAsync(db, "Ingredient Status", "Foreign", CompanyB);
        var wrongCategory = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = "Not a status"
        };
        db.GeneralData.Add(wrongCategory);
        await db.SaveChangesAsync();

        var response = await new GetRawMaterialOptionsHandler(db, UserA())
            .Handle(new GetRawMaterialOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Active", "Inactive" },
            response.IngredientStatuses.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_IngredientSources_AreOwnCompanyProductRowsOnly()
    {
        var db = await CreateDbAsync(UserA());
        await SeedGeneralDataAsync(db, "Ingredient Source", "Plant Based");
        await SeedGeneralDataAsync(db, "Ingredient Source", "Animal Based");
        await SeedGeneralDataAsync(db, "Ingredient Source", "Foreign", CompanyB);

        var response = await new GetRawMaterialOptionsHandler(db, UserA())
            .Handle(new GetRawMaterialOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Animal Based", "Plant Based" },
            response.IngredientSources.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_Manufacturers_ListsRowsThatCarryAManufacturerName()
    {
        var db = await CreateDbAsync(UserA());
        await SeedManufacturerAsync(db);
        db.ManufacturerSuppliers.Add(new ManufacturerSupplierEntity
        {
            CompanyId = CompanyA,
            Type = ManufacturerSupplierType.SupplierOnly,
            SupplierName = "Supplies Only",
            SupplierAddress = "Jalan Gombak 2"
        });
        await db.SaveChangesAsync();

        var response = await new GetRawMaterialOptionsHandler(db, UserA())
            .Handle(new GetRawMaterialOptionsQuery(), CancellationToken.None);

        Assert.Equal("Santan Foods", Assert.Single(response.Manufacturers).Name);
    }

    [Fact]
    public async Task Handle_Companies_ListsEveryLiveCompany()
    {
        var db = await CreateDbAsync(UserA());
        var zulu = await SeedCompanyAsync(db, "Zulu Trading");
        var alpha = await SeedCompanyAsync(db, "Alpha Trading");

        var response = await new GetRawMaterialOptionsHandler(db, UserA())
            .Handle(new GetRawMaterialOptionsQuery(), CancellationToken.None);

        // Shared rows name OTHER companies, so the picker is deliberately not tenant-scoped.
        Assert.Equal(
            new[] { "Alpha Trading", "Zulu Trading" },
            response.Companies.Select(row => row.Name).ToArray());
        Assert.Contains(response.Companies, row => row.Id == zulu);
        Assert.Contains(response.Companies, row => row.Id == alpha);
    }
}
