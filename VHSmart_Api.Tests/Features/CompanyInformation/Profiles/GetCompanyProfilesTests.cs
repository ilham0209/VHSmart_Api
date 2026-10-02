using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

public class GetCompanyProfilesTests
{
    [Fact]
    public async Task Handle_PlatformAdmin_ShowsEveryCompanyWithCertificationBody()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Alpha Foods");
        await ProfileTestData.SeedCompanyAsync(db, "Beta Foods");
        // Touch a row so the audit hook stamps SysDateModified (fresh seeds only carry
        // SysDateCreated); that is the value the spec list shows as Modified Date.
        var alpha = await db.Companies.SingleAsync(row => row.Name == "Alpha Foods");
        alpha.Telephone = "0300000001";
        await db.SaveChangesAsync();

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery(), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        var rows = grid.Data.ToList();
        Assert.Equal(new[] { "Alpha Foods", "Beta Foods" }, rows.Select(row => row.CompanyName));
        Assert.Equal("Alpha Foods CB", rows[0].CertificationBodyName);
        Assert.False(string.IsNullOrWhiteSpace(rows[0].BusinessRegistrationNo));
        Assert.NotNull(rows[0].ModifiedDate);
    }

    [Fact]
    public async Task Handle_CompanyUser_ShowsOnlyOwnCompany()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        await ProfileTestData.SeedCompanyAsync(db, "Other Foods");

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("Own Foods", row.CompanyName);
        Assert.Equal(user.CompanyId, row.Id);
    }

    [Fact]
    public async Task Handle_ViewAllCompaniesToken_StillStaysOnOwnCompany()
    {
        // ViewAll widens the EF tenant filter but the handler scopes the query itself, so a
        // company user cannot read the other tenants' rows from this screen either.
        var user = ProfileTestData.CompanyUser(viewAllCompanies: true);
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        await ProfileTestData.SeedCompanyAsync(db, "Other Foods");

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(user.CompanyId, row.Id);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersByName()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Alpha Foods");
        await ProfileTestData.SeedCompanyAsync(db, "Beta Foods");

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery
            {
                Request = new DataGridRequest { SearchTerm = "beta" }
            }, CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal("Beta Foods", row.CompanyName);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersByBusinessRegistrationNo()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Alpha Foods");
        var betaId = await ProfileTestData.SeedCompanyAsync(db, "Beta Foods");
        var betaNumber = await ProfileTestData.BusinessRegistrationNoAsync(db, betaId);

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery
            {
                Request = new DataGridRequest { SearchTerm = betaNumber }
            }, CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(betaId, row.Id);
    }

    [Fact]
    public async Task Handle_NoSort_DefaultsToCompanyNameAscending()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Zeta Foods");
        await ProfileTestData.SeedCompanyAsync(db, "Alpha Foods");

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Alpha Foods", "Zeta Foods" }, grid.Data.Select(row => row.CompanyName));
    }

    [Fact]
    public async Task Handle_ClientSort_OverridesTheDefaultOrder()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Alpha Foods");
        await ProfileTestData.SeedCompanyAsync(db, "Zeta Foods");

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery
            {
                Request = new DataGridRequest
                {
                    SortBy = nameof(GetCompanyProfilesResponse.CompanyName),
                    SortDescending = true
                }
            }, CancellationToken.None);

        Assert.Equal(new[] { "Zeta Foods", "Alpha Foods" }, grid.Data.Select(row => row.CompanyName));
    }

    [Fact]
    public async Task Handle_SoftDeletedCompany_IsHidden()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var deletedId = await ProfileTestData.SeedCompanyAsync(db, "Gone Foods");
        var company = await db.Companies.SingleAsync(row => row.Id == deletedId);
        db.Companies.Remove(company);
        await db.SaveChangesAsync();

        var grid = await new GetCompanyProfilesHandler(db, user)
            .Handle(new GetCompanyProfilesQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }
}
