using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class GetCompanyByIdTests
{
    [Fact]
    public async Task Handle_ExistingCompany_ReturnsFieldsAndBrands()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var natural = await CompanyTestData.SeedBrandAsync(db, "NATURAL");
        var rtw = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", [rtw, natural]);

        var response = await new GetCompanyByIdHandler(db, user)
            .Handle(new GetCompanyByIdQuery(companyId), CancellationToken.None);

        Assert.Equal(companyId, response.Id);
        Assert.Equal("Acme Foods", response.Name);
        Assert.Equal("Muslim Owner", response.OwnerStatus);
        Assert.Equal("50000", response.PostCode);
        Assert.True(response.IsActive);
        Assert.Equal(["NATURAL", "RTW"], response.Brands.Select(brand => brand.Name));
        Assert.Equal(rtw, response.Brands[1].Id);
    }

    [Fact]
    public async Task Handle_SoftDeletedBrandLink_IsNotReturned()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var brandId = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Acme Foods", [brandId]);
        var link = await db.CompanyBrands.SingleAsync(row => row.CompanyId == companyId);
        link.IsDeleted = true;
        await db.SaveChangesAsync();

        var response = await new GetCompanyByIdHandler(db, user)
            .Handle(new GetCompanyByIdQuery(companyId), CancellationToken.None);

        Assert.Empty(response.Brands);
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyByIdHandler(db, user)
                .Handle(new GetCompanyByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedCompany_ThrowsNotFound()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Acme Foods");
        var company = await db.Companies.SingleAsync(row => row.Id == companyId);
        company.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyByIdHandler(db, user)
                .Handle(new GetCompanyByIdQuery(companyId), CancellationToken.None));
    }
}
