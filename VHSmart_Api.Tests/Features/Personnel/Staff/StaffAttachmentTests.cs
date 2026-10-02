using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class StaffAttachmentTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartStaffAttachmentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public StaffAttachmentTests()
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

    private static async Task<StaffEntity> SeedStaffAsync(
        TestableVHSmartDbContext db, Guid companyId)
    {
        var generalData = new List<GeneralDataEntity>
        {
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Title of Honour", Name = "Mr" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Department", Name = "Production" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Designation", Name = "Halal Executive" }
        };
        db.GeneralData.AddRange(generalData);
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Email = "staff@verify.my",
            TitleId = generalData[0].Id,
            Name = "Siti Aminah",
            DepartmentId = generalData[1].Id,
            DesignationId = generalData[2].Id
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static async Task<Guid> SeedDocumentTypeAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        SupportingDocumentForView forView = SupportingDocumentForView.AllStaff,
        string documentType = "Medical Report")
    {
        var row = new SupportingDocumentEntity
        {
            CompanyId = companyId,
            ForView = forView,
            DocumentType = documentType,
            DocumentSequence = 1,
            IsMandatory = false
        };
        db.SupportingDocuments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static IFormFile DocumentFile(string fileName = "report.pdf")
    {
        var bytes = (byte[])[37, 80, 68, 70]; // %PDF magic bytes
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "application/pdf"
        };
        return file;
    }

    private UploadStaffAttachmentHandler NewUploadHandler(
        TestableVHSmartDbContext db, TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadStaffAttachmentHandler>.Instance);

    [Fact]
    public async Task Upload_ValidFile_StoresRowWithBytesAndTypeName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var documentTypeId = await SeedDocumentTypeAsync(db, CompanyA);

        var response = await NewUploadHandler(db, user).Handle(
            new UploadStaffAttachmentCommand(staff.Id, documentTypeId, DocumentFile()),
            CancellationToken.None);

        Assert.Equal("Medical Report", response.DocumentType);
        Assert.Equal("report.pdf", response.FileName);

        var stored = await db.StaffAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(staff.Id, stored.StaffId);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
    }

    [Fact]
    public async Task Upload_DocumentTypeNotForAllStaff_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var trainingType = await SeedDocumentTypeAsync(
            db, CompanyA, SupportingDocumentForView.Training, "Attendance Sheet");

        var result = await new UploadStaffAttachmentValidator(db, user).ValidateAsync(
            new UploadStaffAttachmentCommand(staff.Id, trainingType, DocumentFile()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Type of document is not for All Staff.");
    }

    [Fact]
    public async Task Upload_ForeignDocumentType_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var foreignType = await SeedDocumentTypeAsync(db, CompanyB, documentType: "Foreign Type");

        var result = await new UploadStaffAttachmentValidator(db, user).ValidateAsync(
            new UploadStaffAttachmentCommand(staff.Id, foreignType, DocumentFile()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Type of document not found.");
    }

    [Fact]
    public async Task Upload_UnknownStaff_ThrowsNotFoundAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var documentTypeId = await SeedDocumentTypeAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewUploadHandler(db, user).Handle(
                new UploadStaffAttachmentCommand(Guid.NewGuid(), documentTypeId, DocumentFile()),
                CancellationToken.None));

        Assert.Equal(0, await db.StaffAttachments.IgnoreQueryFilters().CountAsync());
        var files = Directory.Exists(_storageRoot) ? Directory.GetFiles(_storageRoot) : Array.Empty<string>();
        Assert.Empty(files);
    }

    [Fact]
    public async Task List_ShowsRowsWithDocumentTypeNameNumberedFromOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var documentTypeId = await SeedDocumentTypeAsync(db, CompanyA);
        await NewUploadHandler(db, user).Handle(
            new UploadStaffAttachmentCommand(staff.Id, documentTypeId, DocumentFile()),
            CancellationToken.None);

        var result = await new GetAllStaffAttachmentsHandler(db, user).Handle(
            new GetAllStaffAttachmentsQuery(staff.Id, new DataGridRequest()),
            CancellationToken.None);

        var row = Assert.Single(result.Data);
        Assert.Equal(1, row.No);
        Assert.Equal("Medical Report", row.DocumentType);
        Assert.Equal("report.pdf", row.FileName);
    }

    [Fact]
    public async Task List_UnknownOrForeignStaff_ThrowsNotFound()
    {
        var user = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, user);
        await db.Database.EnsureCreatedAsync();
        var foreign = await SeedStaffAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAllStaffAttachmentsHandler(db, user).Handle(
                new GetAllStaffAttachmentsQuery(Guid.NewGuid(), new DataGridRequest()),
                CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAllStaffAttachmentsHandler(db, user).Handle(
                new GetAllStaffAttachmentsQuery(foreign.Id, new DataGridRequest()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ExistingRow_SoftDeletesIt()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var documentTypeId = await SeedDocumentTypeAsync(db, CompanyA);
        var attachment = await NewUploadHandler(db, user).Handle(
            new UploadStaffAttachmentCommand(staff.Id, documentTypeId, DocumentFile()),
            CancellationToken.None);

        await new DeleteStaffAttachmentHandler(db, user)
            .Handle(new DeleteStaffAttachmentCommand(attachment.Id), CancellationToken.None);

        Assert.True((await db.StaffAttachments.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Delete_UnknownOrForeignRow_ThrowsNotFound()
    {
        var user = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, user);
        await db.Database.EnsureCreatedAsync();
        var foreignStaff = await SeedStaffAsync(db, CompanyB);
        var foreignType = await SeedDocumentTypeAsync(db, CompanyB);
        var foreignAttachment = new StaffAttachmentEntity
        {
            CompanyId = CompanyB,
            StaffId = foreignStaff.Id,
            DocumentTypeId = foreignType,
            Document = new()
            {
                FileName = "report.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf"
            }
        };
        db.StaffAttachments.Add(foreignAttachment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteStaffAttachmentHandler(db, user)
                .Handle(new DeleteStaffAttachmentCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteStaffAttachmentHandler(db, user)
                .Handle(new DeleteStaffAttachmentCommand(foreignAttachment.Id), CancellationToken.None));
        Assert.False((await db.StaffAttachments.IgnoreQueryFilters().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Document_WithBytes_StreamsThem()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var documentTypeId = await SeedDocumentTypeAsync(db, CompanyA);
        var attachment = await NewUploadHandler(db, user).Handle(
            new UploadStaffAttachmentCommand(staff.Id, documentTypeId, DocumentFile()),
            CancellationToken.None);

        var response = await new GetStaffAttachmentDocumentHandler(db, user, _storage)
            .Handle(new GetStaffAttachmentDocumentQuery(attachment.Id), CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        byte[] bytes;
        using (response.Content)
        {
            using var memory = new MemoryStream();
            await response.Content.CopyToAsync(memory);
            bytes = memory.ToArray();
        }

        Assert.Equal((byte[])[37, 80, 68, 70], bytes);
    }

    [Fact]
    public async Task Document_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetStaffAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetStaffAttachmentDocumentQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
