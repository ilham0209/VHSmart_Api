using VHSmart_Api.Features.Audit.AuditPrefix;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class GetAuditPrefixOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsOwnCompanyBrandRowsOrderedByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBrandAsync(db, name: "ZENITH");
        await SeedBrandAsync(db, name: "MRS Brand");

        var response = await new GetAuditPrefixOptionsHandler(db, user).Handle(
            new GetAuditPrefixOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "MRS Brand", "ZENITH" },
            response.Brands.Select(row => row.Name));
        Assert.All(response.Brands, row => Assert.NotEqual(Guid.Empty, row.Id));
    }

    [Fact]
    public async Task Handle_ExcludesBrandsOfAnotherCompany()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBrandAsync(db, name: "NATURAL");
        await SeedBrandAsync(db, name: "Foreign Brand", companyId: CompanyB);

        var response = await new GetAuditPrefixOptionsHandler(db, user).Handle(
            new GetAuditPrefixOptionsQuery(), CancellationToken.None);

        var brand = Assert.Single(response.Brands);
        Assert.Equal("NATURAL", brand.Name);
    }

    [Fact]
    public async Task Handle_ExcludesNonBrandGeneralData()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedBrandAsync(db, name: "NATURAL");
        await SeedNonBrandGeneralDataAsync(db);

        var response = await new GetAuditPrefixOptionsHandler(db, user).Handle(
            new GetAuditPrefixOptionsQuery(), CancellationToken.None);

        var brand = Assert.Single(response.Brands);
        Assert.Equal("NATURAL", brand.Name);
    }

    [Fact]
    public async Task Handle_CompanyWithoutBrands_ReturnsEmptyList()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await new GetAuditPrefixOptionsHandler(db, user).Handle(
            new GetAuditPrefixOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Brands);
    }
}
