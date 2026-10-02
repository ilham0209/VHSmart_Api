using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Premise Attachment tab (spec 7.7, D-15, D-22): upload per document type (PDF only,
// upsert on the type), the five fixed rows with N/A for untouched types, per-row
// Valid / Expired and the streamed download.
public class PremiseAttachmentTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartPremiseAttachmentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public PremiseAttachmentTests() => _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IFormFile DocumentFile(string fileName = "halal-cert.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    private UploadPremiseAttachmentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadPremiseAttachmentHandler>.Instance);

    private static UploadPremiseAttachmentCommand UploadCommand(
        Guid premiseId,
        string documentType = "HALAL CERTIFICATE",
        DateTime? expiryDate = null,
        string? referenceNo = null,
        IFormFile? file = null) =>
        new(premiseId, documentType, expiryDate, referenceNo, file ?? DocumentFile());

    [Fact]
    public async Task Upload_NewType_StoresRowAndReturnsTheFullTab()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        var response = await UploadHandler(db, user).Handle(
            UploadCommand(premiseId, expiryDate: new DateTime(2099, 6, 1),
                referenceNo: "HC-001"),
            CancellationToken.None);

        // The tab always answers the five D-15 types in order; the uploaded one carries
        // its file, the rest stay N/A (spec 7.7).
        Assert.Equal(5, response.Count);
        Assert.Equal(
            PremiseDocumentStatusCalculator.RequiredDocumentTypes,
            response.Select(row => row.DocumentType).ToList());
        Assert.All(response.Where(row => row.Id is null), row =>
        {
            Assert.Null(row.FileName);
            Assert.Null(row.DocumentStatus);
        });
        var uploaded = response.Single(row => row.DocumentType == "HALAL CERTIFICATE");
        Assert.NotNull(uploaded.Id);
        Assert.Equal(5, uploaded.No);
        Assert.Equal("halal-cert.pdf", uploaded.FileName);
        Assert.Equal(new DateOnly(2099, 6, 1), uploaded.ExpiryDate);
        Assert.Equal("HC-001", uploaded.ReferenceNo);
        Assert.Equal(HalalStatus.Valid, uploaded.DocumentStatus);

        var stored = await db.PremiseAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(user.CompanyId, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.Equal("HALAL CERTIFICATE", stored.DocumentType);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
    }

    [Fact]
    public async Task Upload_ExistingType_ReplacesTheRowAndTheBytes()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        await UploadHandler(db, user).Handle(
            UploadCommand(premiseId, expiryDate: new DateTime(2099, 6, 1),
                referenceNo: "FIRST"),
            CancellationToken.None);
        var first = await db.PremiseAttachments.SingleAsync();
        var firstKey = first.Document.StorageKey;

        // The request's casing is normalized to the D-15 spelling, so it hits the same row.
        var response = await UploadHandler(db, user).Handle(
            UploadCommand(premiseId, "halal certificate", expiryDate: new DateTime(2020, 1, 1),
                referenceNo: "SECOND", file: DocumentFile("halal-v2.pdf")),
            CancellationToken.None);

        // One current row per (PremiseId, DocumentType) (Database.md 7): the second upload
        // replaced the first one's file instead of adding a second row for the type.
        Assert.Equal(1, await db.PremiseAttachments.CountAsync());
        var stored = await db.PremiseAttachments.SingleAsync();
        Assert.Equal("HALAL CERTIFICATE", stored.DocumentType);
        Assert.Equal("halal-v2.pdf", stored.Document.FileName);
        Assert.Equal(new DateTime(2020, 1, 1), stored.ExpiryDate);
        Assert.Equal("SECOND", stored.ReferenceNo);
        Assert.False(File.Exists(Path.Combine(_storageRoot, firstKey)));
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));

        var row = response.Single(r => r.DocumentType == "HALAL CERTIFICATE");
        Assert.Equal("halal-v2.pdf", row.FileName);
        Assert.Equal(HalalStatus.Expired, row.DocumentStatus);
        Assert.Equal(1, response.Count(r => r.FileName == "halal-v2.pdf"));
        Assert.Null(response.Single(r => r.DocumentType == "BUSINESS LICENSE").Id);
    }

    [Fact]
    public async Task Upload_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(foreignId), CancellationToken.None));

        Assert.Equal(0, await db.PremiseAttachments.CountAsync());
    }

    [Fact]
    public async Task Upload_NonPdfFile_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        // D-22: premise attachments are PDF only (BusinessRuleException -> 422).
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(premiseId, file: DocumentFile("payload.docx")),
                CancellationToken.None));

        Assert.Equal(0, await db.PremiseAttachments.CountAsync());
    }

    [Fact]
    public async Task Upload_MissingFile_ThrowsBusinessRule()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                new UploadPremiseAttachmentCommand(
                    premiseId, "HALAL CERTIFICATE", null, null, File: null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingOrUnknownFields_Fail()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var validator = new UploadPremiseAttachmentValidator();

        var noPremise = await validator.ValidateAsync(
            UploadCommand(Guid.Empty));
        var noType = await validator.ValidateAsync(
            UploadCommand(premiseId) with { DocumentType = " " });
        var unknownType = await validator.ValidateAsync(
            UploadCommand(premiseId) with { DocumentType = "STAMP" });
        var longReference = await validator.ValidateAsync(
            UploadCommand(premiseId) with { ReferenceNo = new string('x', 101) });
        var noFile = await validator.ValidateAsync(
            UploadCommand(premiseId) with { File = null });

        Assert.False(noPremise.IsValid);
        Assert.False(noType.IsValid);
        Assert.Contains(noType.Errors,
            failure => failure.ErrorMessage == "Document type is required.");
        Assert.False(unknownType.IsValid);
        Assert.Contains(unknownType.Errors,
            failure => failure.ErrorMessage == "Document type is invalid.");
        Assert.False(longReference.IsValid);
        Assert.Contains(longReference.Errors,
            failure => failure.ErrorMessage
                == "Reference number must be 100 characters or fewer.");
        Assert.False(noFile.IsValid);
        Assert.Contains(noFile.Errors,
            failure => failure.ErrorMessage == "Document is required.");
    }

    [Fact]
    public async Task GetAttachments_AllUntouched_ShowN_ARows()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);

        var response = await new GetPremiseAttachmentsHandler(db, user)
            .Handle(new GetPremiseAttachmentsQuery(premiseId), CancellationToken.None);

        Assert.Equal(5, response.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, response.Select(row => row.No));
        Assert.All(response, row =>
        {
            Assert.Null(row.Id);
            Assert.Null(row.FileName);
            Assert.Null(row.ExpiryDate);
            Assert.Null(row.ReferenceNo);
            Assert.Null(row.DocumentStatus);
        });
    }

    [Fact]
    public async Task GetAttachments_RowStatuses_FollowD04AndSentinelRules()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        await PremiseTestData.SeedPremiseAttachmentAsync(
            db, user.CompanyId, premiseId, "BUSINESS LICENSE",
            expiryDate: new DateTime(2020, 1, 1));
        await PremiseTestData.SeedPremiseAttachmentAsync(
            db, user.CompanyId, premiseId, "HALAL CERTIFICATE");
        await PremiseTestData.SeedPremiseAttachmentAsync(
            db, user.CompanyId, premiseId, "FOSIM",
            expiryDate: new DateTime(9999, 12, 31));

        var response = await new GetPremiseAttachmentsHandler(db, user)
            .Handle(new GetPremiseAttachmentsQuery(premiseId), CancellationToken.None);

        Assert.Equal(HalalStatus.Expired,
            response.Single(row => row.DocumentType == "BUSINESS LICENSE").DocumentStatus);
        Assert.Equal(new DateOnly(2020, 1, 1),
            response.Single(row => row.DocumentType == "BUSINESS LICENSE").ExpiryDate);
        Assert.Equal(HalalStatus.Valid,
            response.Single(row => row.DocumentType == "HALAL CERTIFICATE").DocumentStatus);
        Assert.Null(response.Single(row => row.DocumentType == "HALAL CERTIFICATE").ExpiryDate);
        // D-04: the sentinel expiry displays empty and never reads Expired.
        Assert.Equal(HalalStatus.Valid,
            response.Single(row => row.DocumentType == "FOSIM").DocumentStatus);
        Assert.Null(response.Single(row => row.DocumentType == "FOSIM").ExpiryDate);
        Assert.Null(
            response.Single(row => row.DocumentType == "COMPANY INFORMATION").DocumentStatus);
    }

    [Fact]
    public async Task GetAttachments_UnknownOrForeignPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var foreignId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseAttachmentsHandler(db, user)
                .Handle(new GetPremiseAttachmentsQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseAttachmentsHandler(db, user)
                .Handle(new GetPremiseAttachmentsQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Document_StreamsTheStoredBytes()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var document = await _storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "halal.pdf", "application/pdf");
        var attachmentId = await PremiseTestData.SeedPremiseAttachmentAsync(
            db, user.CompanyId, premiseId, "HALAL CERTIFICATE", document: document);

        var response = await new GetPremiseAttachmentDocumentHandler(db, user, _storage)
            .Handle(new GetPremiseAttachmentDocumentQuery(premiseId, attachmentId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Document_UnknownOrForeignOrWrongPremise_ThrowsNotFound()
    {
        var user = PremiseTestData.CompanyUser();
        var foreignUser = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        var premiseId = await PremiseTestData.SeedPremiseAsync(db, user.CompanyId);
        var otherPremiseId = await PremiseTestData.SeedPremiseAsync(
            db, user.CompanyId, "Other premise");
        var attachmentId = await PremiseTestData.SeedPremiseAttachmentAsync(
            db, user.CompanyId, premiseId, "FOSIM");
        var foreignPremiseId = await PremiseTestData.SeedPremiseAsync(
            db, foreignUser.CompanyId, "Foreign premise");
        var foreignAttachmentId = await PremiseTestData.SeedPremiseAttachmentAsync(
            db, foreignUser.CompanyId, foreignPremiseId, "FOSIM");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetPremiseAttachmentDocumentQuery(
                    premiseId, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetPremiseAttachmentDocumentQuery(
                    otherPremiseId, attachmentId), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetPremiseAttachmentDocumentHandler(db, user, _storage)
                .Handle(new GetPremiseAttachmentDocumentQuery(
                    foreignPremiseId, foreignAttachmentId), CancellationToken.None));
    }
}
