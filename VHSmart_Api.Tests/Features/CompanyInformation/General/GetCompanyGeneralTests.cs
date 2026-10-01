using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.General;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.General;

public class GetCompanyGeneralTests
{
    [Fact]
    public async Task Handle_OwnCompany_ReturnsGeneralFieldsAndCertificationBodyName()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user, "Acme Foods");

        var response = await new GetCompanyGeneralHandler(db, user)
            .Handle(new GetCompanyGeneralQuery(), CancellationToken.None);

        Assert.Equal(user.CompanyId, response.Id);
        Assert.Equal("Acme Foods", response.Name);
        Assert.Equal("Acme Foods CB", response.CertificationBodyName);
        Assert.Equal("Muslim Owner", response.OwnerStatus);
        Assert.Equal("50000", response.PostCode);
        Assert.Equal("Gombak", response.District);
        Assert.NotNull(response.DateOfEstablishment);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyGeneralHandler(db, user)
                .Handle(new GetCompanyGeneralQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OtherTenantCompany_IsNotVisibleEvenWithViewAll()
    {
        var user = CompanyGeneralTestData.CompanyUser(viewAllCompanies: true);
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOtherCompanyAsync(db, "Other Co");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyGeneralHandler(db, user)
                .Handle(new GetCompanyGeneralQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedOwnCompany_ThrowsNotFound()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user);
        var company = await db.Companies.SingleAsync(row => row.Id == user.CompanyId);
        company.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyGeneralHandler(db, user)
                .Handle(new GetCompanyGeneralQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task GetOptions_ReturnsD13Lists()
    {
        var response = await new GetCompanyGeneralOptionsHandler()
            .Handle(new GetCompanyGeneralOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Companies Commission Of Malaysia" },
            response.RegistrationTypes);
        Assert.Equal(new[] { "Muslim Owner" }, response.OwnerStatuses);
        Assert.Equal(new[] { "Medium Small Company" }, response.IndustrySizes);
        Assert.Equal(new[] { "Overseas", "Domestic" }, response.Markets);
    }
}
