using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Users;

public class CreateActivationTokenTests
{
    [Fact]
    public async Task Handle_NotYetActivated_IssuesNewTokenAndInvalidatesOldOne()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "newbie@example.com", company);
        user.IsActive = false;
        user.ActivatedAt = null;
        var oldToken = new UserTokenEntity
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.Activation,
            TokenHash = OneTimeToken.Hash("OLD-RAW-TOKEN"),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.UserTokens.Add(oldToken);
        await db.SaveChangesAsync();

        var response = await new CreateActivationTokenHandler(db, caller, UsersTestData.Config())
            .Handle(new CreateActivationTokenCommand(user.Id), default);

        Assert.Equal(user.Id, response.UserId);

        var tokens = await db.UserTokens.IgnoreQueryFilters()
            .Where(row => row.UserId == user.Id)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        // Only the newest link may ever work: the previous row is soft-deleted.
        var stale = Assert.Single(tokens, row => row.Id == oldToken.Id);
        Assert.True(stale.IsDeleted);
        var fresh = Assert.Single(tokens, row => row.Id != oldToken.Id);
        Assert.False(fresh.IsDeleted);
        Assert.Null(fresh.UsedAt);
        Assert.Equal(OneTimeToken.Hash(response.ActivationToken), fresh.TokenHash);
    }

    [Fact]
    public async Task Handle_AlreadyActivated_ThrowsBusinessRule()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);
        var company = await UsersTestData.AddCompanyAsync(db, "ALPHA SDN BHD");
        var user = await UsersTestData.AddUserAsync(db, "done@example.com", company);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CreateActivationTokenHandler(db, caller, UsersTestData.Config())
                .Handle(new CreateActivationTokenCommand(user.Id), default));

        Assert.Equal("This account is already activated.", exception.Message);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var caller = UsersTestData.PlatformAdmin(Guid.NewGuid());
        var db = await UsersTestData.CreateDbAsync(databaseName, caller);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateActivationTokenHandler(db, caller, UsersTestData.Config())
                .Handle(new CreateActivationTokenCommand(Guid.NewGuid()), default));
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
            new CreateActivationTokenHandler(db, companyAdmin, UsersTestData.Config())
                .Handle(new CreateActivationTokenCommand(userB.Id), default));
    }

    [Fact]
    public void Validator_MissingId_Fails()
    {
        var validator = new CreateActivationTokenValidator();

        var result = validator.Validate(new CreateActivationTokenCommand(Guid.Empty));

        Assert.False(result.IsValid);
    }
}
