using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Companies;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class GetAllCompaniesTests
{
    [Fact]
    public async Task Handle_OneRowPerLinkedBrand_AndSingleRowWithoutBrand()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var rtw = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var serunai = await CompanyTestData.SeedBrandAsync(db, "SERUNAI");
        var branded = await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", [rtw, serunai]);
        var plain = await CompanyTestData.SeedCompanyAsync(db, "Beta Trading");

        var response = await new GetAllCompaniesHandler(db, user)
            .Handle(new GetAllCompaniesQuery(), CancellationToken.None);

        Assert.Equal(3, response.TotalRecords);
        Assert.Equal(3, response.Data.Count());

        var acme = response.Data.Where(row => row.Id == branded).ToList();
        Assert.Equal(["RTW", "SERUNAI"], acme.Select(row => row.Brand));
        Assert.All(acme, row =>
        {
            Assert.Equal("Acme Foods", row.Name);
            Assert.Equal("Acme Foods CB", row.CertificationBody);
        });

        var beta = Assert.Single(response.Data, row => row.Id == plain);
        Assert.Null(beta.Brand);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersCompanies()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var rtw = await CompanyTestData.SeedBrandAsync(db, "RTW");
        await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", [rtw]);
        await CompanyTestData.SeedCompanyAsync(db, "Beta Trading");
        var query = new GetAllCompaniesQuery();
        query.Request.SearchTerm = "acme";

        var response = await new GetAllCompaniesHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Single(response.Data);
        Assert.Equal("Acme Foods", response.Data.Single().Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedBrandLink_FallsBackToSingleRow()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var brandId = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", [brandId]);
        var link = await db.CompanyBrands.SingleAsync(row => row.CompanyId == companyId);
        link.IsDeleted = true;
        await db.SaveChangesAsync();

        var response = await new GetAllCompaniesHandler(db, user)
            .Handle(new GetAllCompaniesQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("Acme Foods", row.Name);
        Assert.Null(row.Brand);
    }

    [Fact]
    public async Task Handle_PagingCountsExpandedRows()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var brands = new[]
        {
            await CompanyTestData.SeedBrandAsync(db, "RTW"),
            await CompanyTestData.SeedBrandAsync(db, "SERUNAI"),
            await CompanyTestData.SeedBrandAsync(db, "NATURAL")
        };
        await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", brands);
        var query = new GetAllCompaniesQuery();
        query.Request.PageSize = 2;

        var response = await new GetAllCompaniesHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Equal(3, response.TotalRecords);
        Assert.Equal(2, response.TotalPages);
        Assert.Equal(2, response.Data.Count());
        Assert.True(response.HasNextPage);
        Assert.False(response.HasPreviousPage);
    }

    [Fact]
    public async Task Handle_SortByNameDescending_ReturnsHighestFirst()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        await CompanyTestData.SeedCompanyAsync(db, "Alpha Co");
        await CompanyTestData.SeedCompanyAsync(db, "Zulu Co");
        var query = new GetAllCompaniesQuery();
        query.Request.SortBy = "Name";
        query.Request.SortDescending = true;

        var response = await new GetAllCompaniesHandler(db, user)
            .Handle(query, CancellationToken.None);

        Assert.Equal(["Zulu Co", "Alpha Co"], response.Data.Select(row => row.Name));
    }
}
