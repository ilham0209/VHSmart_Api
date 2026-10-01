using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VHSmart_Api.Features.Auth.Activation;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Auth.Activation;

public class ActivateTests
{
    [Fact]
    public async Task Handle_ValidToken_ActivatesAccountAndReturnsGeneratedPasswordOnce()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = await AddPendingUserAsync(db, "newbie@example.com");
        var rawToken = IssueToken(db, user);

        var response = await new ActivateHandler(db)
            .Handle(new ActivateCommand(rawToken), default);

        Assert.Equal(user.Id, response.UserId);
        Assert.Equal("newbie@example.com", response.Email);
        Assert.Equal(16, response.Password.Length);

        var stored = await db.Users.AsNoTracking().SingleAsync(row => row.Id == user.Id);
        Assert.True(stored.IsActive);
        Assert.NotNull(stored.ActivatedAt);
        Assert.True(stored.MustChangePassword);
        // Only the hash is stored; the returned password verifies against it (CodingRules 8.3).
        Assert.NotEqual(response.Password, stored.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            UserPasswordHasher.Verify(stored, stored.PasswordHash, response.Password));

        var token = await db.UserTokens.AsNoTracking().SingleAsync();
        Assert.NotNull(token.UsedAt);
    }

    [Fact]
    public async Task Handle_SameTokenTwice_IsRejected()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = await AddPendingUserAsync(db, "newbie@example.com");
        var rawToken = IssueToken(db, user);

        await new ActivateHandler(db).Handle(new ActivateCommand(rawToken), default);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ActivateHandler(db).Handle(new ActivateCommand(rawToken), default));
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsNotFoundWithFixedMessage()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new ActivateHandler(db)
                .Handle(new ActivateCommand("DEADBEEF"), default));

        Assert.Equal("Invalid or expired activation token.", exception.Message);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = await AddPendingUserAsync(db, "newbie@example.com");
        var rawToken = IssueToken(db, user, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ActivateHandler(db).Handle(new ActivateCommand(rawToken), default));
    }

    [Fact]
    public async Task Handle_TokenOfSoftDeletedUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = await AddPendingUserAsync(db, "newbie@example.com");
        var rawToken = IssueToken(db, user);
        user.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ActivateHandler(db).Handle(new ActivateCommand(rawToken), default));
    }

    [Fact]
    public async Task Handle_CaseSensitivity_TrimmedTokenStillMatches()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = await AddPendingUserAsync(db, "newbie@example.com");
        var rawToken = IssueToken(db, user);

        var response = await new ActivateHandler(db)
            .Handle(new ActivateCommand($"  {rawToken}  "), default);

        Assert.Equal(user.Id, response.UserId);
    }

    [Fact]
    public void Validator_EmptyToken_Fails()
    {
        var validator = new ActivateValidator();

        var result = validator.Validate(new ActivateCommand(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Token"
                && error.ErrorMessage == "Activation token is required.");
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(string databaseName)
    {
        var db = TestDbFactory.Create(databaseName);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<UserEntity> AddPendingUserAsync(
        TestableVHSmartDbContext db,
        string email)
    {
        var user = new UserEntity
        {
            Name = "NEW USER",
            Email = email,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            IsActive = false,
            PasswordHash = string.Empty
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static string IssueToken(
        TestableVHSmartDbContext db,
        UserEntity user,
        DateTime? expiresAt = null)
    {
        var (rawToken, tokenHash, _) = OneTimeToken.Issue(Config());
        db.UserTokens.Add(new UserTokenEntity
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.Activation,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1)
        });
        db.SaveChanges();
        return rawToken;
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
}
