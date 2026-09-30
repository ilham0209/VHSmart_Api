using VHSmart_Api.Features.Account.Profile;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Account.Profile;

public class GetProfileTests
{
    [Fact]
    public async Task Handle_ReturnsNameEmailAndRole()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true);
        var currentUser = new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId);
        var handler = new GetProfileHandler(db, currentUser);

        var response = await handler.Handle(new GetProfileQuery(), CancellationToken.None);

        Assert.Equal("TEST USER", response.Name);
        Assert.Equal("profile@example.com", response.Email);
        Assert.Equal("VH Smart Admin", response.RoleName);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var currentUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());
        var handler = new GetProfileHandler(db, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProfileQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: false);
        var currentUser = new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId);
        var handler = new GetProfileHandler(db, currentUser);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetProfileQuery(), CancellationToken.None));
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(string databaseName)
    {
        var db = TestDbFactory.Create(databaseName);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static UserEntity AddUser(TestableVHSmartDbContext db, bool isActive)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = "profile@example.com",
            IsActive = isActive,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
