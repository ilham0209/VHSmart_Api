using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class UpdateSupportingDocumentTests : IDisposable
{
    private const string SequenceExistsMessage = "Data document sequence exist. Please check the existing data.";

    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartSupportingDocUpdateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public UpdateSupportingDocumentTests()
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

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IFormFile TemplateFile(string fileName = "template.pdf", byte[]? content = null)
    {
        var bytes = content ?? (byte[])[37, 80, 68, 70];
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Template", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "application/pdf"
        };
        return file;
    }

    private static CreateSupportingDocumentCommand NewCommand(
        SupportingDocumentForView forView = SupportingDocumentForView.SopHas,
        string documentType = "Halal Procedure",
        int sequence = 1,
        bool isMandatory = false,
        string? description = "SOP template",
        IFormFile? template = null) =>
        new(forView, documentType, sequence, isMandatory, description, template);

    private static UpdateSupportingDocumentCommand Command(
        Guid id,
        SupportingDocumentForView? forView = SupportingDocumentForView.SopHas,
        string documentType = "Halal Procedure",
        int? sequence = 1,
        bool isMandatory = false,
        string? description = "SOP template",
        IFormFile? template = null) =>
        new(id, forView, documentType, sequence, isMandatory, description, template);

    private CreateSupportingDocumentHandler NewCreateHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<CreateSupportingDocumentHandler>.Instance);

    private UpdateSupportingDocumentHandler NewHandler(TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UpdateSupportingDocumentHandler>.Instance);

    private async Task<(TestableVHSmartDbContext Db, Guid RowId)> SeedAsync(
        SupportingDocumentForView forView = SupportingDocumentForView.SopHas,
        int sequence = 1,
        IFormFile? template = null)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var created = await NewCreateHandler(db, user)
            .Handle(NewCommand(forView: forView, sequence: sequence, template: template), CancellationToken.None);
        return (db, created.Id);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesEveryField()
    {
        var (db, rowId) = await SeedAsync();
        var user = UserA();

        var response = await NewHandler(db, user).Handle(
            Command(
                rowId,
                forView: SupportingDocumentForView.AllStaff,
                documentType: "Appointment Letter",
                sequence: 15,
                isMandatory: true,
                description: "Staff attachment type"),
            CancellationToken.None);

        Assert.Equal(SupportingDocumentForView.AllStaff, response.ForView);
        Assert.Equal("Appointment Letter", response.DocumentType);
        Assert.Equal(15, response.DocumentSequence);
        Assert.True(response.IsMandatory);
        Assert.Equal("Staff attachment type", response.Description);

        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(15, stored.DocumentSequence);
        Assert.True(stored.IsMandatory);
    }

    [Fact]
    public async Task Handle_DuplicateSequence_ExcludesTheRowItself()
    {
        var (db, rowId) = await SeedAsync(sequence: 4);
        var user = UserA();

        await NewHandler(db, user).Handle(
            Command(rowId, sequence: 4, documentType: "Renamed Type"), CancellationToken.None);

        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed Type", stored.DocumentType);
    }

    [Fact]
    public async Task Handle_DuplicateSequenceOnAnotherRow_ThrowsConflictWithTheManualMessage()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await NewCreateHandler(db, user).Handle(
            NewCommand(forView: SupportingDocumentForView.Training, sequence: 1), CancellationToken.None);
        var second = await NewCreateHandler(db, user).Handle(
            NewCommand(forView: SupportingDocumentForView.Training, sequence: 2), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            NewHandler(db, user).Handle(
                Command(second.Id, forView: SupportingDocumentForView.Training, sequence: 1),
                CancellationToken.None));

        Assert.Equal(SequenceExistsMessage, exception.Message);
    }

    [Fact]
    public async Task Handle_WithTemplate_ReplacesItAndDeletesThePreviousBytes()
    {
        var (db, rowId) = await SeedAsync(template: TemplateFile());
        var user = UserA();
        var previousKey = (await db.SupportingDocuments.AsNoTracking().SingleAsync()).Template!.StorageKey;
        Assert.True(File.Exists(Path.Combine(_storageRoot, previousKey)));

        var response = await NewHandler(db, user).Handle(
            Command(rowId, template: TemplateFile("second.pdf")), CancellationToken.None);

        Assert.Equal("second.pdf", response.TemplateFileName);
        Assert.False(File.Exists(Path.Combine(_storageRoot, previousKey)));

        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Template!.StorageKey)));
    }

    [Fact]
    public async Task Handle_WithoutTemplate_KeepsTheCurrentOne()
    {
        var (db, rowId) = await SeedAsync(template: TemplateFile());
        var user = UserA();
        var storageKey = (await db.SupportingDocuments.AsNoTracking().SingleAsync()).Template!.StorageKey;

        var response = await NewHandler(db, user).Handle(
            Command(rowId, documentType: "Halal Procedure Revised", template: null), CancellationToken.None);

        Assert.Equal("template.pdf", response.TemplateFileName);
        Assert.True(File.Exists(Path.Combine(_storageRoot, storageKey)));
    }

    [Fact]
    public async Task Handle_MovingToNonSopView_DropsTheTemplateAndItsBytes()
    {
        var (db, rowId) = await SeedAsync(
            forView: SupportingDocumentForView.SopHas, template: TemplateFile());
        var user = UserA();
        var storageKey = (await db.SupportingDocuments.AsNoTracking().SingleAsync()).Template!.StorageKey;

        var response = await NewHandler(db, user).Handle(
            Command(rowId, forView: SupportingDocumentForView.Training, template: null),
            CancellationToken.None);

        // Spec 5.5: only SOP views carry a template.
        Assert.Null(response.TemplateFileName);
        var stored = await db.SupportingDocuments.AsNoTracking().SingleAsync();
        Assert.Null(stored.Template);
        Assert.False(File.Exists(Path.Combine(_storageRoot, storageKey)));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var (db, _) = await SeedAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewHandler(db, UserA()).Handle(Command(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreign = await NewCreateHandler(db, userA)
            .Handle(NewCommand(documentType: "Company B type"), CancellationToken.None);

        var dbB = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyB));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewHandler(dbB, UserA()).Handle(Command(foreign.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_TemplateOnNonSopView_Fails()
    {
        var validator = new UpdateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(Command(
            Guid.NewGuid(),
            forView: SupportingDocumentForView.HalalApplication,
            template: TemplateFile()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Template");
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var validator = new UpdateSupportingDocumentValidator();

        var result = await validator.ValidateAsync(new UpdateSupportingDocumentCommand(
            Guid.NewGuid(), null, string.Empty, null, false, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "ForView");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "DocumentType");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "DocumentSequence");
    }
}
