using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Ihc;

public class MinutesMeetingAttachmentTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartIhcAttachmentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public MinutesMeetingAttachmentTests() => _storage = new LocalFileStorage(_storageRoot);

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

    private static IFormFile DocumentFile(string fileName = "minutes.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    [Fact]
    public async Task Upload_ValidFile_StoresRowAndBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        var command = new UploadMinutesMeetingAttachmentCommand(meetingId, DocumentFile());

        var response = await UploadHandler(db, user).Handle(command, CancellationToken.None);

        var attachment = Assert.Single(response.Attachments);
        Assert.Equal("minutes.pdf", attachment.FileName);
        Assert.Equal(1, attachment.No);
        var stored = await db.MinutesMeetingAttachments.AsNoTracking().SingleAsync();
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document!.StorageKey)));
        Assert.Equal(user.UserId, stored.SysUserCreated);
    }

    [Fact]
    public async Task Upload_WithoutFile_StoresRowWithoutBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);

        var response = await UploadHandler(db, user).Handle(
            new UploadMinutesMeetingAttachmentCommand(meetingId, null),
            CancellationToken.None);

        var attachment = Assert.Single(response.Attachments);
        Assert.Null(attachment.FileName);
        var stored = await db.MinutesMeetingAttachments.AsNoTracking().SingleAsync();
        Assert.Null(stored.Document);
    }

    [Fact]
    public async Task Upload_DisallowedFileType_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        var command = new UploadMinutesMeetingAttachmentCommand(
            meetingId, DocumentFile("payload.exe"));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(command, CancellationToken.None));

        Assert.Equal(0, await db.MinutesMeetingAttachments.CountAsync());
    }

    [Fact]
    public async Task Upload_UnknownOrForeignMeeting_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignMeeting = await MinutesMeetingTestData.SeedMeetingAsync(
            db, foreignCompany);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user).Handle(
                new UploadMinutesMeetingAttachmentCommand(Guid.NewGuid(), null),
                CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user).Handle(
                new UploadMinutesMeetingAttachmentCommand(foreignMeeting, null),
                CancellationToken.None));

        Assert.Equal(0, await db.MinutesMeetingAttachments.CountAsync());
    }

    [Fact]
    public async Task Delete_SoftDeletesTheAttachment()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, meetingId);

        await new DeleteMinutesMeetingAttachmentHandler(db, user)
            .Handle(new DeleteMinutesMeetingAttachmentCommand(meetingId, attachmentId),
                CancellationToken.None);

        Assert.Equal(0, await db.MinutesMeetingAttachments.CountAsync());
        Assert.True(await db.MinutesMeetingAttachments.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == attachmentId && row.IsDeleted));
    }

    [Fact]
    public async Task Delete_UnknownOrWrongMeetingOrForeign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        var otherMeetingId = await MinutesMeetingTestData.SeedMeetingAsync(
            db, CompanyA, "Other meeting");
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, meetingId);
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignMeeting = await MinutesMeetingTestData.SeedMeetingAsync(db, foreignCompany);
        var foreignAttachmentId = await SeedAttachmentAsync(db, foreignCompany, foreignMeeting);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMinutesMeetingAttachmentHandler(db, user)
                .Handle(new DeleteMinutesMeetingAttachmentCommand(meetingId, Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMinutesMeetingAttachmentHandler(db, user)
                .Handle(new DeleteMinutesMeetingAttachmentCommand(otherMeetingId, attachmentId),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteMinutesMeetingAttachmentHandler(db, user)
                .Handle(new DeleteMinutesMeetingAttachmentCommand(
                    foreignMeeting, foreignAttachmentId),
                    CancellationToken.None));

        Assert.True(await db.MinutesMeetingAttachments.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == attachmentId && !row.IsDeleted));
    }

    [Fact]
    public async Task Document_StreamsTheStoredBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        var document = await _storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "minutes.pdf", "application/pdf");
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, meetingId, document);

        var response = await new GetMinutesMeetingAttachmentDocumentHandler(db, user, _storage)
            .Handle(new GetMinutesMeetingAttachmentDocumentQuery(meetingId, attachmentId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Document_WithoutBytesOrForeignOrUnknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var meetingId = await MinutesMeetingTestData.SeedMeetingAsync(db, CompanyA);
        db.MinutesMeetingAttachments.Add(new MinutesMeetingAttachmentEntity
        {
            CompanyId = CompanyA,
            MinutesMeetingId = meetingId
        });
        await db.SaveChangesAsync();
        var filelessId = (await db.MinutesMeetingAttachments.SingleAsync()).Id;
        var foreignCompany = await MinutesMeetingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignMeeting = await MinutesMeetingTestData.SeedMeetingAsync(db, foreignCompany);
        var foreignAttachmentId = await SeedAttachmentAsync(db, foreignCompany, foreignMeeting);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMinutesMeetingAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetMinutesMeetingAttachmentDocumentQuery(meetingId, filelessId),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMinutesMeetingAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetMinutesMeetingAttachmentDocumentQuery(meetingId, Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetMinutesMeetingAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetMinutesMeetingAttachmentDocumentQuery(
                    foreignMeeting, foreignAttachmentId),
                    CancellationToken.None));
    }

    private UploadMinutesMeetingAttachmentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadMinutesMeetingAttachmentHandler>.Instance);

    private static async Task<Guid> SeedAttachmentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid meetingId,
        StoredFile? document = null)
    {
        var row = new MinutesMeetingAttachmentEntity
        {
            CompanyId = companyId,
            MinutesMeetingId = meetingId,
            Document = document ?? new StoredFile
            {
                FileName = "seeded.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf",
                SizeBytes = 4
            }
        };
        db.MinutesMeetingAttachments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
