using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.CompanyInformation.HalalPolicy;

public class CreateHalalPolicyTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartHalalPolicyCreateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public CreateHalalPolicyTests()
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

    private static IFormFile DocumentFile(string fileName = "policy.pdf", byte[]? content = null)
    {
        var bytes = content ?? (byte[])[37, 80, 68, 70]; // %PDF magic bytes, enough for storage
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "application/pdf"
        };
        return file;
    }

    private static async Task<CreateHalalPolicyCommand> CommandAsync(
        TestableVHSmartDbContext db,
        IFormFile? file = null)
    {
        var schemeId = await db.Schemes.Select(scheme => scheme.Id).FirstAsync();
        return new CreateHalalPolicyCommand(schemeId, new DateTime(2026, 1, 15), file ?? DocumentFile());
    }

    private CreateHalalPolicyHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<CreateHalalPolicyHandler>.Instance);

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithDocumentAndCompanyFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await NewHandler(db, user)
            .Handle(await CommandAsync(db), CancellationToken.None);

        Assert.Equal("policy.pdf", response.FileName);
        Assert.Equal(new DateTime(2026, 1, 15), response.PolicyDate);
        Assert.False(string.IsNullOrWhiteSpace(response.Scheme));

        var stored = await db.HalalPolicies.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal("application/pdf", stored.Document.ContentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_DuplicateScheme_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db);
        await NewHandler(db, user).Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameSchemeInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();

        var companyARow = await NewHandler(db, userA)
            .Handle(await CommandAsync(db), CancellationToken.None);

        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        var companyBRow = await NewHandler(dbB, userB)
            .Handle(await CommandAsync(dbB), CancellationToken.None);

        Assert.NotEqual(companyARow.Id, companyBRow.Id);
        Assert.Equal(CompanyB, (await dbB.HalalPolicies.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var command = await CommandAsync(db, DocumentFile("installer.exe"));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(command, CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        Assert.Equal(0, await db.HalalPolicies.IgnoreQueryFilters().CountAsync());
        var files = Directory.Exists(_storageRoot) ? Directory.GetFiles(_storageRoot) : Array.Empty<string>();
        Assert.Empty(files);
    }

    [Fact]
    public async Task Handle_FileOverTenMegabytes_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var oversized = new byte[10 * 1024 * 1024 + 1];
        var command = await CommandAsync(db, DocumentFile("policy.pdf", oversized));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(command, CancellationToken.None));

        Assert.Contains("exceeds the limit", exception.Message);
        Assert.Equal(0, await db.HalalPolicies.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateHalalPolicyValidator(db);

        var result = await validator.ValidateAsync(new CreateHalalPolicyCommand(
            Guid.Empty, default, null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateHalalPolicyCommand.SchemeId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateHalalPolicyCommand.PolicyDate));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateHalalPolicyCommand.File));
    }

    [Fact]
    public async Task Validator_UnknownScheme_Fails()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateHalalPolicyValidator(db);

        var result = await validator.ValidateAsync(new CreateHalalPolicyCommand(
            Guid.NewGuid(), new DateTime(2026, 1, 15), DocumentFile()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme not found.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CreateDbAsync(UserA());
        var validator = new CreateHalalPolicyValidator(db);

        var result = await validator.ValidateAsync(await CommandAsync(db));

        Assert.True(result.IsValid);
    }
}
