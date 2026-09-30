using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Features.Auth.SwitchCompany;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Auth;

public class SwitchCompanyTests
{
    [Fact]
    public async Task Handle_MemberCompany_IssuesTokenForTargetCompany()
    {
        var db = await CreateDbAsync();
        var user = AddUser(db);
        var firstCompany = Guid.NewGuid();
        var secondCompany = Guid.NewGuid();
        AddMembership(db, user.Id, firstCompany, isDefault: true);
        AddMembership(db, user.Id, secondCompany, isDefault: false);
        await db.SaveChangesAsync();

        var tokens = new RecordingTokenService();
        var handler = new SwitchCompanyHandler(db, CurrentUser(user), tokens);

        var session = await handler.Handle(new SwitchCompanyCommand(secondCompany), CancellationToken.None);

        Assert.Equal(secondCompany, session.ActiveCompanyId);
        Assert.Equal(secondCompany, tokens.LastCompanyId);
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(2, session.Companies.Count);
    }

    [Fact]
    public async Task Handle_CompanyNotOnMembership_ThrowsNotFound()
    {
        var db = await CreateDbAsync();
        var user = AddUser(db);
        AddMembership(db, user.Id, Guid.NewGuid(), isDefault: true);
        await db.SaveChangesAsync();

        var handler = new SwitchCompanyHandler(db, CurrentUser(user), new RecordingTokenService());

        // 404, not 403: the caller must not learn which company ids exist (CodingRules 9).
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SwitchCompanyCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedMembership_ThrowsNotFound()
    {
        var db = await CreateDbAsync();
        var user = AddUser(db);
        var company = Guid.NewGuid();
        var membership = new UserCompanyEntity { UserId = user.Id, CompanyId = company, IsDefault = true };
        db.UserCompanies.Add(membership);
        await db.SaveChangesAsync();

        db.UserCompanies.Remove(membership);
        await db.SaveChangesAsync();

        var handler = new SwitchCompanyHandler(db, CurrentUser(user), new RecordingTokenService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SwitchCompanyCommand(company), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var db = await CreateDbAsync();
        var user = AddUser(db);
        var company = Guid.NewGuid();
        AddMembership(db, user.Id, company, isDefault: true);
        user.IsActive = false;
        await db.SaveChangesAsync();

        var handler = new SwitchCompanyHandler(db, CurrentUser(user), new RecordingTokenService());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SwitchCompanyCommand(company), CancellationToken.None));

        Assert.Equal(LoginMessages.Inactive, exception.Message);
    }

    [Fact]
    public async Task Handle_UserNotInDatabase_ThrowsUnauthorized()
    {
        var db = await CreateDbAsync();
        var ghost = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid(), isPlatformAdmin: true);

        var handler = new SwitchCompanyHandler(db, ghost, new RecordingTokenService());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new SwitchCompanyCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyCompanyId_Fails()
    {
        var result = new SwitchCompanyValidator().Validate(new SwitchCompanyCommand(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SwitchCompanyCommand.CompanyId));
    }

    private static ICurrentUser CurrentUser(UserEntity user) =>
        new TestCurrentUser(user.Id.ToString(), Guid.Empty, user.RoleId, user.IsPlatformAdmin);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync()
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName());
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static UserEntity AddUser(TestableVHSmartDbContext db)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = "user@example.com",
            IsActive = true,
            IsPlatformAdmin = true,
            RoleId = RoleSeedData.VhSmartAdminRoleId
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, "Passw0rd!");
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static void AddMembership(
        TestableVHSmartDbContext db,
        Guid userId,
        Guid companyId,
        bool isDefault) =>
        db.UserCompanies.Add(new UserCompanyEntity
        {
            UserId = userId,
            CompanyId = companyId,
            IsDefault = isDefault
        });

    private sealed class RecordingTokenService : IJwtTokenService
    {
        public Guid? LastCompanyId { get; private set; }

        public IssuedToken CreateToken(
            Guid userId,
            Guid companyId,
            Guid roleId,
            bool isPlatformAdmin,
            bool viewAllCompanies)
        {
            LastCompanyId = companyId;
            return new IssuedToken("token-for-tests", DateTime.UtcNow.AddMinutes(60));
        }
    }
}
