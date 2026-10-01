using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class GetUserOptionsTests
{
    [Fact]
    public async Task Handle_PlatformAdmin_ReturnsAllCompaniesAndAllRoles()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        _ = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        _ = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");

        var options = await new GetUserOptionsHandler(db, caller)
            .Handle(new GetUserOptionsQuery(), default);

        Assert.Equal(2, options.Companies.Count);
        // The three seeded system roles (D-19) arrive with EnsureCreated.
        Assert.Equal(3, options.Roles.Count);
        Assert.Contains(options.Roles, role => role.Name == "VH Smart Admin");
        Assert.Contains(options.Roles, role => role.Name == "Auditor / Chief Auditor");
        Assert.Contains(options.Roles, role => role.Name == "Restaurant / Premise Manager");
    }

    [Fact]
    public async Task Handle_CompanyAdmin_ReturnsOwnCompanyAndSystemRolesOnly()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var ownCompany = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        _ = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");

        // A per-company role (Database.md 5) must not leak into the company admin's picker.
        var companyRole = new RoleEntity
        {
            Name = "BETA ROLE",
            CompanyId = await db.Companies
                .Where(company => company.Name == "BETA SDN BHD")
                .Select(company => company.Id)
                .SingleAsync()
        };
        db.Roles.Add(companyRole);
        await db.SaveChangesAsync();

        var companyAdmin = UsersTestData.CompanyAdmin(ownCompany);
        var options = await new GetUserOptionsHandler(db, companyAdmin)
            .Handle(new GetUserOptionsQuery(), default);

        var company = Assert.Single(options.Companies);
        Assert.Equal(ownCompany, company.Id);
        Assert.Equal("ALPHA SDN BHD", company.Name);

        Assert.Equal(3, options.Roles.Count);
        Assert.DoesNotContain(options.Roles, role => role.Id == companyRole.Id);
    }

    [Fact]
    public async Task Handle_SoftDeletedCompanyAndRole_AreHidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyRow = await db.Companies.SingleAsync(row => row.Id == company);
        db.Companies.Remove(companyRow);
        var role = new RoleEntity { Name = "TEMP ROLE" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Roles.Remove(role);
        await db.SaveChangesAsync();

        var options = await new GetUserOptionsHandler(db, caller)
            .Handle(new GetUserOptionsQuery(), default);

        Assert.Empty(options.Companies);
        Assert.Equal(3, options.Roles.Count);
        Assert.DoesNotContain(options.Roles, row => row.Id == role.Id);
    }
}
