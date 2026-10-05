using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

public class GetRawMaterialByIdTests
{
    [Fact]
    public async Task Handle_ExistingRow_ReturnsEveryFormFieldAndTheCompanyList()
    {
        var db = await CreateDbAsync(UserA());
        var zuluPartner = await SeedCompanyAsync(db, "Zulu Partner");
        var alphaPartner = await SeedCompanyAsync(db, "Alpha Partner");
        var id = await SeedRowAsync(
            db,
            ingredient: "Rice Flour",
            ingredientCode: "RM-001",
            accessibleCompanyIds: [zuluPartner, alphaPartner],
            commercialName: "Rice Flour 1kg",
            scientificName: "Oryza sativa");

        var response = await new GetRawMaterialByIdHandler(db)
            .Handle(new GetRawMaterialByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal(RawMaterialCategory.Core, response.Category);
        Assert.Equal("Rice Flour", response.Ingredient);
        Assert.Equal("RM-001", response.IngredientCode);
        Assert.Equal("Rice Flour 1kg", response.CommercialName);
        Assert.Equal("Oryza sativa", response.ScientificName);
        Assert.Equal("Santan Foods", response.ManufacturerName);
        Assert.False(response.IsPackagingMaterial);

        // The "List of Company" table of the Accessible For picker, ordered by company name.
        Assert.Equal(
            new[] { "Alpha Partner", "Zulu Partner" },
            response.AccessibleFor.Select(row => row.CompanyName).ToArray());
        Assert.Equal(
            new[] { alphaPartner, zuluPartner },
            response.AccessibleFor.Select(row => row.CompanyId).ToArray());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRawMaterialByIdHandler(db)
                .Handle(new GetRawMaterialByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnsharedRowOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetRawMaterialByIdHandler(db)
                .Handle(new GetRawMaterialByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowSharedToTheCaller_IsReadable()
    {
        var db = await CreateDbAsync(UserB());
        var sharedId = await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);

        var response = await new GetRawMaterialByIdHandler(db)
            .Handle(new GetRawMaterialByIdQuery(sharedId), CancellationToken.None);

        Assert.Equal(sharedId, response.Id);
        Assert.Equal("Company B", Assert.Single(response.AccessibleFor).CompanyName);
    }
}
