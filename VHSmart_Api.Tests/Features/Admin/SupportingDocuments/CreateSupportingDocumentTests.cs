using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class CreateSupportingDocumentTests : IDisposable
{
    private const string SequenceExistsMessage = "Data document sequence exist. Please check the existing data.";

    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartSupportingDocCreateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public CreateSupportingDocumentTests()
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

    private static IFormFile TemplateFile(string fileName = "template.pdf", byte[]? content = null)
    {
        var bytes = content ?? (byte[])[37, 80, 68, 70]; // %PDF magic bytes, enough for storage
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Template", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "application/pdf"
        };
        return file;
    }

    private static CreateSupportingDocumentCommand Command(
        SupportingDocumentForView? forView = SupportingDocumentForView.SopHas,
        string documentType = "Halal Procedure",
        int? sequence = 1,
        bool isMandatory = false,
        string? description = "SOP template",
        IFormFile? template = null) =>
        new(forView, documentType, sequence, isMandatory, description, template);

    private CreateSupportingDocumentHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<CreateSupportingDocumentHandler>.Instance);

    [Fact]
    public async Task Handle_ValidCommand_StoresRowWithCompanyFromUser()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await NewHandler(db, user)
            .Handle(Command(), CancellationToken.None);

        Assert.Equal("Halal Procedure", response.DocumentType);
        Assert.Equal(SupportingDocumentForView.SopHas, response.ForView);
        Assert.Equal(1, response.DocumentSequence);
        Assert.False(response.IsMandatory);
        Assert.Null(response.TemplateFileName);

        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        // CompanyId comes from the JWT (CodingRules 8.1), never from the body.
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Handle_WithTemplate_StoresBytesAndMetadata()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await NewHandler(db, user)
            .Handle(Command(template: TemplateFile()), CancellationToken.None);

        Assert.Equal("template.pdf", response.TemplateFileName);

        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.Equal("application/pdf", stored.Template!.ContentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Template.StorageKey)));
    }

    [Fact]
    public async Task Handle_DuplicateSequence_ThrowsConflictWithTheManualMessage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await NewHandler(db, user).Handle(Command(sequence: 4), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(Command(sequence: 4), CancellationToken.None));

        Assert.Equal(SequenceExistsMessage, exception.Message);
    }

    [Fact]
    public async Task Handle_SameSequenceInAnotherView_DoesNotConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await NewHandler(db, user).Handle(
            Command(forView: SupportingDocumentForView.Training, sequence: 1), CancellationToken.None);

        await NewHandler(db, user).Handle(
            Command(forView: SupportingDocumentForView.SopHas, sequence: 1), CancellationToken.None);

        Assert.Equal(2, await db.SupportingDocuments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_SameSequenceInAnotherCompany_DoesNotConflict()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        await NewHandler(db, userA).Handle(Command(sequence: 7), CancellationToken.None);

        var userB = UserB();
        var dbB = TestDbFactory.Create(databaseName, userB);
        await NewHandler(dbB, userB).Handle(Command(sequence: 7), CancellationToken.None);

        Assert.Equal(CompanyB, (await dbB.SupportingDocuments.AsNoTracking().SingleAsync()).CompanyId);
    }

    [Fact]
    public async Task Handle_MissingForView_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(Command(forView: null), CancellationToken.None));

        Assert.Equal("For View is required.", exception.Message);
        Assert.Equal(0, await db.SupportingDocuments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_MissingSequence_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(Command(sequence: null), CancellationToken.None));

        Assert.Equal("Document Sequence is required.", exception.Message);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            NewHandler(db, user).Handle(Command(template: TemplateFile("setup.exe")), CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        Assert.Equal(0, await db.SupportingDocuments.IgnoreQueryFilters().CountAsync());
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
            NewHandler(db, user).Handle(Command(template: TemplateFile("big.pdf", oversized)), CancellationToken.None));

        Assert.Contains("exceeds the limit", exception.Message);
        Assert.Equal(0, await db.SupportingDocuments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(new CreateSupportingDocumentCommand(
            null, string.Empty, null, false, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ForView");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "DocumentType");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "DocumentSequence");
    }

    [Fact]
    public async Task Validator_UnknownForView_Fails()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(forView: (SupportingDocumentForView)99));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ForView");
    }

    [Fact]
    public async Task Validator_DocumentTypeLongerThan200_Fails()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(documentType: new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "DocumentType");
    }

    [Fact]
    public async Task Validator_DescriptionLongerThan1000_Fails()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(description: new string('x', 1001)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Description");
    }

    [Fact]
    public async Task Validator_TemplateOnNonSopView_Fails()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(
            forView: SupportingDocumentForView.RawMaterial, template: TemplateFile()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Template");
    }

    [Fact]
    public async Task Validator_SopViewWithTemplate_Passes()
    {
        var validator = new CreateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(
            forView: SupportingDocumentForView.SopIhcs, template: TemplateFile()));

        Assert.True(result.IsValid);
    }
}
