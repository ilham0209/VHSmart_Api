using Microsoft.AspNetCore.Identity;
using VHSmart_Api.Features.Account.Password;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Account.Password;

public class ChangePasswordTests
{
    private const string OldPassword = "Passw0rd!";

    private const string NewPassword = "N3wPassw0rd!";

    [Fact]
    public async Task Handle_ValidPassword_UpdatesHashAndClearsMustChange()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true, mustChangePassword: true);
        var handler = NewHandler(db, user);

        var response = await handler.Handle(
            new ChangePasswordCommand(OldPassword, NewPassword, NewPassword), CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(user.MustChangePassword);
        Assert.Equal(
            PasswordVerificationResult.Success,
            UserPasswordHasher.Verify(user, user.PasswordHash, NewPassword));
    }

    [Fact]
    public async Task Handle_WrongOldPassword_ThrowsBusinessRuleAndKeepsHash()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true, mustChangePassword: false);
        var hashBefore = user.PasswordHash;
        var handler = NewHandler(db, user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(
                new ChangePasswordCommand("wrong-old", NewPassword, NewPassword), CancellationToken.None));

        Assert.Equal("Old password is incorrect.", exception.Message);
        Assert.Equal(hashBefore, user.PasswordHash);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var currentUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());
        var handler = new ChangePasswordHandler(db, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new ChangePasswordCommand(OldPassword, NewPassword, NewPassword), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: false, mustChangePassword: false);
        var handler = NewHandler(db, user);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new ChangePasswordCommand(OldPassword, NewPassword, NewPassword), CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyPasswords_Fail()
    {
        Assert.False(new ChangePasswordValidator().Validate(
            new ChangePasswordCommand(string.Empty, NewPassword, NewPassword)).IsValid);

        Assert.False(new ChangePasswordValidator().Validate(
            new ChangePasswordCommand(OldPassword, string.Empty, NewPassword)).IsValid);

        Assert.False(new ChangePasswordValidator().Validate(
            new ChangePasswordCommand(OldPassword, NewPassword, string.Empty)).IsValid);
    }

    [Fact]
    public void Validator_ConfirmDoesNotMatchNew_Fails()
    {
        var result = new ChangePasswordValidator().Validate(
            new ChangePasswordCommand(OldPassword, NewPassword, "different"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Confirm Password does not match New Password.");
    }

    private static ChangePasswordHandler NewHandler(TestableVHSmartDbContext db, UserEntity user)
    {
        var currentUser = new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId);
        return new ChangePasswordHandler(db, currentUser);
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(string databaseName)
    {
        var db = TestDbFactory.Create(databaseName);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static UserEntity AddUser(
        TestableVHSmartDbContext db,
        bool isActive,
        bool mustChangePassword)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = "password@example.com",
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, OldPassword);
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
