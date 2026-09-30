using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class CreateWebLinkTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartWebLinkCreateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public CreateWebLinkTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IFormFile IconFile(string fileName = "icon.png", byte[]? content = null)
    {
        var bytes = content ?? (byte[])[137, 80, 78, 71]; // PNG magic bytes, enough for storage
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "image/png"
        };
        return file;
    }

    private static CreateWebLinkCommand Command(string name = "Verify Halal", IFormFile? file = null) =>
        new(name, "https://verifyhalal.com", "Halal verification", file ?? IconFile());

    private CreateWebLinkHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<CreateWebLinkHandler>.Instance);

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithIconAndCompanyFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await NewHandler(db, user)
            .Handle(Command(), CancellationToken.None);

        Assert.Equal("Verify Halal", response.Name);
        Assert.Equal("icon.png", response.IconFileName);

        var stored = await db.WebLinks.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("image/png", stored.Icon.ContentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Icon.StorageKey)));
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await NewHandler(db, user).Handle(Command(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();

        var companyARow = await NewHandler(db, userA)
            .Handle(Command("Shared name"), CancellationToken.None);

        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        var companyBRow = await NewHandler(dbB, userB)
            .Handle(Command("Shared name"), CancellationToken.None);

        Assert.NotEqual(companyARow.Id, companyBRow.Id);
        Assert.Equal(CompanyB, (await dbB.WebLinks.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(Command(file: IconFile("document.pdf")), CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        Assert.Equal(0, await db.WebLinks.IgnoreQueryFilters().CountAsync());
        var files = Directory.Exists(_storageRoot) ? Directory.GetFiles(_storageRoot) : Array.Empty<string>();
        Assert.Empty(files);
    }

    [Fact]
    public async Task Handle_FileOverTenMegabytes_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var oversized = new byte[10 * 1024 * 1024 + 1];

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(Command(file: IconFile("big.png", oversized)), CancellationToken.None));

        Assert.Contains("exceeds the limit", exception.Message);
        Assert.Equal(0, await db.WebLinks.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var validator = new CreateWebLinkValidator();

        var result = await validator.ValidateAsync(new CreateWebLinkCommand(
            string.Empty, string.Empty, null, null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Webpage");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "File");
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var validator = new CreateWebLinkValidator();

        var result = await validator.ValidateAsync(Command(new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var validator = new CreateWebLinkValidator();

        var result = await validator.ValidateAsync(Command());

        Assert.True(result.IsValid);
    }
}
