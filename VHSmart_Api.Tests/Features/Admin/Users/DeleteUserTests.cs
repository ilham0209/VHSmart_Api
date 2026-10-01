using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class DeleteUserTests
{
    [Fact]
    public async Task Handle_ExistingUser_SoftDeletesUserMembershipsAndTokens()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "leaver@example.com", company);
        db.UserTokens.Add(new UserTokenEntity
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.Activation,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        await new DeleteUserHandler(db, caller).Handle(new DeleteUserCommand(user.Id), default);

        // Soft delete: the rows stay in the store, the global filter hides them.
        var storedUsers = await db.Users.IgnoreQueryFilters()
            .Where(row => row.Id == user.Id)
            .ToListAsync();
        var storedUser = Assert.Single(storedUsers);
        Assert.True(storedUser.IsDeleted);

        var memberships = await db.UserCompanies.IgnoreQueryFilters()
            .Where(row => row.UserId == user.Id)
            .ToListAsync();
        Assert.All(memberships, row => Assert.True(row.IsDeleted));

        var tokens = await db.UserTokens.IgnoreQueryFilters()
            .Where(row => row.UserId == user.Id)
            .ToListAsync();
        Assert.All(tokens, row => Assert.True(row.IsDeleted));
    }

    [Fact]
    public async Task Handle_DeletedUser_DisappearsFromListAndFreesEmail()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "leaver@example.com", company);

        await new DeleteUserHandler(db, caller).Handle(new DeleteUserCommand(user.Id), default);

        Assert.Empty(await db.Users.Where(row => row.Id == user.Id).ToListAsync());

        // Spec 21.9: the e-mail is unique among non-deleted users only.
        var reused = await new CreateUserHandler(db, caller, UsersTestData.Config())
            .Handle(
                new CreateUserCommand(
                    "REPLACEMENT", "leaver@example.com", RoleSeedData.VhSmartAdminRoleId,
                    null, [company]),
                default);
        Assert.NotEqual(user.Id, reused.Id);
        Assert.Equal("leaver@example.com", reused.Email);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteUserHandler(db, caller).Handle(new DeleteUserCommand(Guid.NewGuid()), default));
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
            new DeleteUserHandler(db, companyAdmin).Handle(new DeleteUserCommand(userB.Id), default));

        // 404, and nothing was touched.
        Assert.False((await db.Users.AsNoTracking().SingleAsync(row => row.Id == userB.Id)).IsDeleted);
    }

    [Fact]
    public void Validator_MissingId_Fails()
    {
        var validator = new DeleteUserValidator();

        var result = validator.Validate(new DeleteUserCommand(Guid.Empty));

        Assert.False(result.IsValid);
    }
}
