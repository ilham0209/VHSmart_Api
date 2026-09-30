using VHSmart_Api.Features.Account.ProfilePicture;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Account.ProfilePicture;

public class GetProfilePictureTests
{
    [Fact]
    public async Task Handle_ReturnsContentAndStoredContentType()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var storageRoot = Path.Combine(Path.GetTempPath(), "VHSmartPictureTests", Guid.NewGuid().ToString("N"));
        var storage = new LocalFileStorage(storageRoot);

        try
        {
            var stored = await storage.SaveAsync(
                new MemoryStream([1, 2, 3]), "avatar.png", "image/png", CancellationToken.None);
            var user = AddUser(db);
            user.ProfilePicture = stored;
            await db.SaveChangesAsync();

            var handler = new GetProfilePictureHandler(
                db, new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId), storage);
            var response = await handler.Handle(new GetProfilePictureQuery(), CancellationToken.None);

            Assert.Equal("image/png", response.ContentType);
            using var reader = new StreamReader(response.Content);
            Assert.Equal("\u0001\u0002\u0003", await reader.ReadToEndAsync());
        }
        finally
        {
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Handle_WithoutPicture_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db);
        var handler = new GetProfilePictureHandler(
            db, new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId), new LocalFileStorage(
                Path.Combine(Path.GetTempPath(), $"unused-{Guid.NewGuid():N}")));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetProfilePictureQuery(), CancellationToken.None));
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(string databaseName)
    {
        var db = TestDbFactory.Create(databaseName);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static UserEntity AddUser(TestableVHSmartDbContext db)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = "picture@example.com",
            IsActive = true,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
