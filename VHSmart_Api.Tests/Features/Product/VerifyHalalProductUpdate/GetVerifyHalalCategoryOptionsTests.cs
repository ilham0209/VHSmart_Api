using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.VerifyHalalProductUpdate;
using VHSmart_Api.Shared.Domain.Admin;
using static VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate.VerifyHalalTestData;

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

public class GetVerifyHalalCategoryOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsOwnCompanyProductCategoriesOrderedByName()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductCategoryAsync(db, "Zebra Sauces");
        await SeedProductCategoryAsync(db, "Alpha Sauces");

        var result = await new GetVerifyHalalCategoryOptionsHandler(db, UserA())
            .Handle(new GetVerifyHalalCategoryOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Alpha Sauces", "Zebra Sauces" },
            result.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_ExcludesOtherGroupsAndCategories()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductCategoryAsync(db, "Sauces");
        await SeedGeneralDataAsync(db, GeneralDataGroup.COMPANY, "Brand", "Sereni");
        await SeedGeneralDataAsync(db, GeneralDataGroup.PRODUCT, "Ingredient Status", "Active");

        var result = await new GetVerifyHalalCategoryOptionsHandler(db, UserA())
            .Handle(new GetVerifyHalalCategoryOptionsQuery(), CancellationToken.None);

        Assert.Equal("Sauces", Assert.Single(result).Name);
    }

    [Fact]
    public async Task Handle_ExcludesOtherCompaniesRows()
    {
        var db = await CreateDbAsync(UserA());
        await SeedProductCategoryAsync(db, "Own Sauces");
        await SeedProductCategoryAsync(db, "Foreign Sauces", companyId: CompanyB);

        var result = await new GetVerifyHalalCategoryOptionsHandler(db, UserA())
            .Handle(new GetVerifyHalalCategoryOptionsQuery(), CancellationToken.None);

        Assert.Equal("Own Sauces", Assert.Single(result).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsInvisible()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductCategoryAsync(db, "Sauces");
        var row = await db.GeneralData.SingleAsync(g => g.Id == id);
        row.IsDeleted = true;
        await db.SaveChangesAsync();

        var result = await new GetVerifyHalalCategoryOptionsHandler(db, UserA())
            .Handle(new GetVerifyHalalCategoryOptionsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
