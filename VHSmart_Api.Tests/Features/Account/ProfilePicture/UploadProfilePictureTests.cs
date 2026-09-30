using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Account.ProfilePicture;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Account.ProfilePicture;

public class UploadProfilePictureTests : IDisposable
{
    private const string PngName = "avatar.png";

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartUploadTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public UploadProfilePictureTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_ValidImage_StoresFileAndSetsProfilePicture()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true);
        var handler = NewHandler(db, user);

        var response = await handler.Handle(FileCommand(PngName), CancellationToken.None);

        Assert.Equal(PngName, response.FileName);
        Assert.Equal("image/png", response.ContentType);

        var picture = user.ProfilePicture;
        Assert.NotNull(picture);
        Assert.Equal("image/png", picture.ContentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, picture.StorageKey)));
    }

    [Fact]
    public async Task Handle_ReplacingPicture_DeletesPreviousFile()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true);
        var handler = NewHandler(db, user);
        await handler.Handle(FileCommand(PngName), CancellationToken.None);
        var previousKey = user.ProfilePicture!.StorageKey;

        await handler.Handle(FileCommand("second.png"), CancellationToken.None);

        Assert.Equal("second.png", user.ProfilePicture!.FileName);
        Assert.False(File.Exists(Path.Combine(_storageRoot, previousKey)));
        Assert.True(File.Exists(Path.Combine(_storageRoot, user.ProfilePicture.StorageKey)));
    }

    [Fact]
    public async Task Handle_TooLargeImage_ThrowsBusinessRule()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true);
        var handler = NewHandler(db, user);
        var oversized = new byte[FileValidation.DefaultMaxSizeBytes + 1];

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(FileCommand(PngName, oversized), CancellationToken.None));

        Assert.Contains("exceeds the limit", exception.Message);
        Assert.Null(user.ProfilePicture);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRule()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: true);
        var handler = NewHandler(db, user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(FileCommand("document.pdf"), CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        Assert.Null(user.ProfilePicture);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var currentUser = new TestCurrentUser(Guid.NewGuid().ToString(), Guid.NewGuid());
        var handler = new UploadProfilePictureHandler(db, currentUser, _storage, Logger());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(FileCommand(PngName), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsForbidden()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = await CreateDbAsync(databaseName);
        var user = AddUser(db, isActive: false);
        var handler = NewHandler(db, user);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(FileCommand(PngName), CancellationToken.None));
    }

    [Fact]
    public void Validator_MissingFile_Fails()
    {
        var result = new UploadProfilePictureValidator().Validate(
            new UploadProfilePictureCommand(null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UploadProfilePictureCommand.File));
    }

    private static UploadProfilePictureCommand FileCommand(string fileName, byte[]? content = null)
    {
        var bytes = content ?? [137, 80, 78, 71]; // PNG magic bytes, enough for storage
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, nameof(UploadProfilePictureCommand.File), fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "image/png"
        };
        return new UploadProfilePictureCommand(file);
    }

    private UploadProfilePictureHandler NewHandler(TestableVHSmartDbContext db, UserEntity user)
    {
        var currentUser = new TestCurrentUser(user.Id.ToString(), Guid.NewGuid(), user.RoleId);
        return new UploadProfilePictureHandler(db, currentUser, _storage, Logger());
    }

    private static ILogger<UploadProfilePictureHandler> Logger() =>
        NullLogger<UploadProfilePictureHandler>.Instance;

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
            Email = "picture@example.com",
            IsActive = isActive,
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            ActivatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
