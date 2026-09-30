using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Auth;

public class LoginTests
{
    private const string Password = "Passw0rd!";

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSessionAndResetsLoginState()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        user.FailedLoginCount = 3;
        user.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
        user.LastLoginAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var tokens = new RecordingTokenService();
        var handler = new LoginHandler(db, tokens, Config());

        var session = await handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None);

        Assert.Equal("token-for-tests", session.Token);
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal("TEST USER", session.Name);
        Assert.Equal("VH Smart Admin", session.RoleName);
        Assert.Equal(user.Id, tokens.LastUserId);
        Assert.True(tokens.LastIsPlatformAdmin);
        // Spec 3.1: the super user's Switch Company is ALL, so both flags travel in the token.
        Assert.True(tokens.LastViewAll);
        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public async Task Handle_UnknownEmail_ThrowsUnauthorizedWithGenericMessage()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        AddUser(db, "admin@example.com");
        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("nobody@example.com", Password), CancellationToken.None));

        // Same message as a wrong password: no account enumeration.
        Assert.Equal(LoginMessages.InvalidCredentials, exception.Message);
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedAndIncrementsFailedCount()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", "wrong"), CancellationToken.None));

        var stored = await TestDbFactory.Create(databaseName).Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(1, stored.FailedLoginCount);
        Assert.Null(stored.LockedUntil);
    }

    [Fact]
    public async Task Handle_FailedAttemptsReachLimit_LocksAccountAndClearsCount()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        var handler = new LoginHandler(db, new RecordingTokenService(), Config(maxAttempts: 2, lockoutMinutes: 15));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", "wrong"), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", "wrong"), CancellationToken.None));

        var stored = await TestDbFactory.Create(databaseName).Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.NotNull(stored.LockedUntil);
        Assert.True(stored.LockedUntil > DateTime.UtcNow.AddMinutes(10));
    }

    [Fact]
    public async Task Handle_LockedAccount_RejectsEvenCorrectPassword()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None));

        Assert.Equal(LoginMessages.Locked, exception.Message);
    }

    [Fact]
    public async Task Handle_LockExpired_AcceptsCorrectPasswordAgain()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        user.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        var session = await handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None);

        Assert.Equal(user.Id, session.UserId);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        AddUser(db, "admin@example.com", isActive: false);
        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None));

        Assert.Equal(LoginMessages.Inactive, exception.Message);
    }

    [Fact]
    public async Task Handle_SoftDeletedUser_ThrowsUnauthorized()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        db.Users.Remove(user);
        await db.SaveChangesAsync();

        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmailIsMatchedCaseInsensitively()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "Admin@Example.com");
        var handler = new LoginHandler(db, new RecordingTokenService(), Config());

        var session = await handler.Handle(new LoginCommand("  ADMIN@example.com  ", Password), CancellationToken.None);

        Assert.Equal(user.Id, session.UserId);
    }

    [Fact]
    public async Task Handle_OldFormatHash_IsRehashedOnSuccessfulLogin()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        var v2Hasher = new PasswordHasher<UserEntity>(
            Options.Create(
                new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        user.PasswordHash = v2Hasher.HashPassword(user, Password);
        await db.SaveChangesAsync();
        var oldHash = user.PasswordHash;

        var handler = new LoginHandler(db, new RecordingTokenService(), Config());
        await handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None);

        var stored = await TestDbFactory.Create(databaseName).Users.SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(oldHash, stored.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            UserPasswordHasher.Verify(stored, stored.PasswordHash, Password));
    }

    [Fact]
    public async Task Handle_UserWithoutCompanies_IssuesTokenForEmptyActiveCompany()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        AddUser(db, "admin@example.com");
        var tokens = new RecordingTokenService();
        var handler = new LoginHandler(db, tokens, Config());

        var session = await handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None);

        Assert.Equal(Guid.Empty, session.ActiveCompanyId);
        Assert.Equal(Guid.Empty, tokens.LastCompanyId);
        Assert.Empty(session.Companies);
    }

    [Fact]
    public async Task Handle_DefaultMembership_BecomesTheActiveCompany()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, "admin@example.com");
        var otherCompany = Guid.NewGuid();
        var defaultCompany = Guid.NewGuid();
        db.UserCompanies.AddRange(
            new UserCompanyEntity { UserId = user.Id, CompanyId = otherCompany, IsDefault = false },
            new UserCompanyEntity { UserId = user.Id, CompanyId = defaultCompany, IsDefault = true });
        await db.SaveChangesAsync();

        var tokens = new RecordingTokenService();
        var handler = new LoginHandler(db, tokens, Config());

        var session = await handler.Handle(new LoginCommand("admin@example.com", Password), CancellationToken.None);

        Assert.Equal(defaultCompany, session.ActiveCompanyId);
        Assert.Equal(defaultCompany, tokens.LastCompanyId);
        Assert.Equal(2, session.Companies.Count);
        Assert.Equal(defaultCompany, session.Companies[0].CompanyId);
        Assert.True(session.Companies[0].IsDefault);
    }

    [Fact]
    public void Validator_EmptyEmail_Fails()
    {
        var result = new LoginValidator().Validate(new LoginCommand("", Password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public void Validator_MalformedEmail_Fails()
    {
        var result = new LoginValidator().Validate(new LoginCommand("not-an-email", Password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_EmailLongerThanColumn_Fails()
    {
        var email = $"{new string('a', 250)}@example.com";
        Assert.True(email.Length > 254);

        var result = new LoginValidator().Validate(new LoginCommand(email, Password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_EmptyPassword_Fails()
    {
        var result = new LoginValidator().Validate(new LoginCommand("admin@example.com", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public void Validator_ValidCommand_Passes()
    {
        var result = new LoginValidator().Validate(new LoginCommand("admin@example.com", Password));

        Assert.True(result.IsValid);
    }

    private static IConfiguration Config(int maxAttempts = 5, int lockoutMinutes = 15) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Login:MaxFailedAttempts"] = maxAttempts.ToString(),
                ["Login:LockoutMinutes"] = lockoutMinutes.ToString()
            })
            .Build();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(string databaseName)
    {
        var db = TestDbFactory.Create(databaseName);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static UserEntity AddUser(
        TestableVHSmartDbContext db,
        string email,
        bool isActive = true)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = email,
            IsActive = isActive,
            IsPlatformAdmin = true,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, Password);
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private sealed class RecordingTokenService : IJwtTokenService
    {
        public Guid? LastUserId { get; private set; }

        public Guid? LastCompanyId { get; private set; }

        public bool? LastIsPlatformAdmin { get; private set; }

        public bool? LastViewAll { get; private set; }

        public IssuedToken CreateToken(
            Guid userId,
            Guid companyId,
            Guid roleId,
            bool isPlatformAdmin,
            bool viewAllCompanies)
        {
            LastUserId = userId;
            LastCompanyId = companyId;
            LastIsPlatformAdmin = isPlatformAdmin;
            LastViewAll = viewAllCompanies;
            return new IssuedToken("token-for-tests", DateTime.UtcNow.AddMinutes(60));
        }
    }
}
