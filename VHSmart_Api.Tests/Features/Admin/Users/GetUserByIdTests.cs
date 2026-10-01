using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class GetUserByIdTests
{
    [Fact]
    public async Task Handle_ExistingUser_ReturnsFormPayload()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "siti@example.com", companyA, companyB);
        user.ContactNo = "0123456789";
        await db.SaveChangesAsync();

        var response = await new GetUserByIdHandler(db, caller)
            .Handle(new GetUserByIdQuery(user.Id), default);

        Assert.Equal(user.Id, response.Id);
        Assert.Equal("TEST USER", response.Name);
        Assert.Equal("siti@example.com", response.Email);
        Assert.Equal(RoleSeedData.VhSmartAdminRoleId, response.RoleId);
        Assert.Equal("0123456789", response.ContactNo);
        Assert.True(response.IsActive);
        Assert.NotNull(response.ActivatedAt);
        Assert.Equal(2, response.CompanyIds.Count);
        Assert.Contains(companyA, response.CompanyIds);
        Assert.Contains(companyB, response.CompanyIds);
        // The default membership is listed first, so the form shows it selected first.
        Assert.Equal(companyA, response.CompanyIds[0]);
        Assert.Equal(user.SysDateCreated, response.CreatedDate);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetUserByIdHandler(db, caller).Handle(new GetUserByIdQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_SoftDeletedUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "gone@example.com", company);
        user.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetUserByIdHandler(db, caller).Handle(new GetUserByIdQuery(user.Id), default));
    }

    [Fact]
    public async Task Handle_UserOfAnotherCompany_ThrowsNotFoundForCompanyAdmin()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var seedCaller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, seedCaller);
        var companyA = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var companyB = await UsersTestData.AddCompanyAsync(db, "BETA SDN BHD");
        var userB = await UsersTestData.AddUserAsync(db, "b-user@example.com", companyB);

        var companyAdmin = UsersTestData.CompanyAdmin(companyA);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetUserByIdHandler(db, companyAdmin).Handle(new GetUserByIdQuery(userB.Id), default));
    }
}
