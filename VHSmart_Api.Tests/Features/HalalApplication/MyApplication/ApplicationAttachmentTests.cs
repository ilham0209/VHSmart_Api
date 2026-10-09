using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

// Attachment side tab of spec 12.5: one row per the company's Halal Application document
// type (R-06) with an N/A placeholder until uploaded, the D-22 upload rules, replace-in-
// place per type, the D-26 "attachments stay allowed after submit" exception and the
// streamed download.
public class ApplicationAttachmentTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartApplicationAttachmentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public ApplicationAttachmentTests() => _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IFormFile DocumentFile(string fileName = "supporting-document.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    private static UploadApplicationAttachmentCommand UploadCommand(
        Guid applicationId,
        Guid documentTypeId,
        IFormFile? file = null) =>
        new(applicationId, documentTypeId, file ?? DocumentFile());

    private UploadApplicationAttachmentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadApplicationAttachmentHandler>.Instance);

    private static async Task<(TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId)>
        SeededApplicationAsync()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        return (db, user, applicationId);
    }

    [Fact]
    public async Task List_ReturnsEveryHalalApplicationTypeInOrder_WithNarowsForTheUntouchedOnes()
    {
        var (db, _, applicationId) = await SeededApplicationAsync();
        var specification = await SeedSupportingDocumentAsync(
            db, "PROCESS FLOW", documentSequence: 2);
        var halalForm = await SeedSupportingDocumentAsync(
            db, "HAS", documentSequence: 1);
        await SeedApplicationAttachmentAsync(db, CompanyA, applicationId, halalForm);

        var response = await new GetApplicationAttachmentsHandler(db)
            .Handle(new GetApplicationAttachmentsQuery(applicationId), CancellationToken.None);

        // The side tab is driven by the document-type list in document-sequence order, not
        // by the rows that happen to exist (spec 12.5: N/A until uploaded).
        Assert.Equal(new[] { "HAS", "PROCESS FLOW" }, response.Select(row => row.DocumentType).ToArray());

        var uploaded = response[0];
        Assert.NotNull(uploaded.Id);
        Assert.Equal(halalForm, uploaded.DocumentTypeId);
        Assert.Equal("supporting-document.pdf", uploaded.FileName);

        var untouched = response[1];
        Assert.Null(untouched.Id);
        Assert.Equal(specification, untouched.DocumentTypeId);
        Assert.Null(untouched.FileName);
    }

    [Fact]
    public async Task List_RowOfAnotherForView_DoesNotShow()
    {
        var (db, _, applicationId) = await SeededApplicationAsync();
        await SeedSupportingDocumentAsync(db, "HALAL APPLICATION FORM");
        await SeedSupportingDocumentAsync(
            db, "HALAL CERTIFICATE", forView: SupportingDocumentForView.RawMaterial);

        var response = await new GetApplicationAttachmentsHandler(db)
            .Handle(new GetApplicationAttachmentsQuery(applicationId), CancellationToken.None);

        // R-06: the side tab only offers the types configured for For View = Halal
        // Application - the Raw Material types belong to the raw material screens.
        var row = Assert.Single(response);
        Assert.Equal("HALAL APPLICATION FORM", row.DocumentType);
    }

    [Fact]
    public async Task List_UnknownOrForeignApplication_ThrowsNotFound()
    {
        var (db, _, _) = await SeededApplicationAsync();
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyB);
        var handler = new GetApplicationAttachmentsHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetApplicationAttachmentsQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetApplicationAttachmentsQuery(foreignApplicationId), CancellationToken.None));
    }

    [Fact]
    public async Task Upload_ValidFile_StoresTheRowAndTheBytes()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");

        var response = await UploadHandler(db, user)
            .Handle(UploadCommand(applicationId, documentType), CancellationToken.None);

        var row = Assert.Single(response);
        Assert.NotNull(row.Id);
        Assert.Equal("HAS", row.DocumentType);
        Assert.Equal("supporting-document.pdf", row.FileName);

        var stored = await db.ApplicationAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
    }

    [Fact]
    public async Task Upload_SameTypeTwice_ReplacesTheRowInPlace()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");

        await UploadHandler(db, user)
            .Handle(UploadCommand(applicationId, documentType, DocumentFile("first.pdf")),
                CancellationToken.None);
        await UploadHandler(db, user)
            .Handle(UploadCommand(applicationId, documentType, DocumentFile("second.pdf")),
                CancellationToken.None);

        var stored = await db.ApplicationAttachments.AsNoTracking().SingleAsync();
        Assert.Equal("second.pdf", stored.Document.FileName);
        // The superseded bytes were dropped only after the save succeeded.
        Assert.Single(Directory.GetFiles(_storageRoot));
    }

    [Fact]
    public async Task Upload_ForeignDocumentType_ThrowsTheNotFoundMessage()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var foreignType = await SeedSupportingDocumentAsync(db, "HAS", companyId: CompanyB);

        var validator = new UploadApplicationAttachmentValidator(db, user);
        var result = await validator.ValidateAsync(
            UploadCommand(applicationId, foreignType));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Document type not found.");
    }

    [Fact]
    public async Task Upload_DocumentTypeOfAnotherForView_ThrowsTheNotFoundMessage()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var rawMaterialType = await SeedSupportingDocumentAsync(
            db, "HALAL CERTIFICATE", forView: SupportingDocumentForView.RawMaterial);

        var validator = new UploadApplicationAttachmentValidator(db, user);
        var result = await validator.ValidateAsync(
            UploadCommand(applicationId, rawMaterialType));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Document type not found.");
    }

    [Fact]
    public async Task Upload_DisallowedExtension_ThrowsTheD22Rule()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user)
                .Handle(
                    UploadCommand(applicationId, documentType, DocumentFile("virus.exe")),
                    CancellationToken.None));

        Assert.StartsWith("File type is not allowed.", exception.Message);
        Assert.Empty(Directory.Exists(_storageRoot)
            ? Directory.GetFiles(_storageRoot)
            : []);
    }

    [Fact]
    public async Task Upload_ForeignApplication_ThrowsNotFound()
    {
        var (db, user, _) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user)
                .Handle(UploadCommand(foreignApplicationId, documentType), CancellationToken.None));
    }

    [Fact]
    public async Task Upload_AfterSubmit_IsStillAllowed()
    {
        var (db, user, _) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");
        // D-26: submitted applications are read-only EXCEPT status tagging, certificate
        // number and attachments - so the upload must pass the D-26 guard.
        var submittedId = await SeedApplicationAsync(
            db, CompanyA, status: "PROCESSING AT JAKIM (NEW)");

        var response = await UploadHandler(db, user)
            .Handle(UploadCommand(submittedId, documentType), CancellationToken.None);

        Assert.NotNull(Assert.Single(response).Id);
        Assert.Equal(1, await db.ApplicationAttachments.CountAsync(
            row => row.ApplicationId == submittedId));
    }

    [Fact]
    public async Task Download_StreamsTheStoredFile()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");
        await UploadHandler(db, user)
            .Handle(UploadCommand(applicationId, documentType), CancellationToken.None);
        var attachmentId = (await db.ApplicationAttachments.AsNoTracking().SingleAsync()).Id;

        var response = await new GetApplicationAttachmentDocumentHandler(db, user, _storage)
            .Handle(
                new GetApplicationAttachmentDocumentQuery(applicationId, attachmentId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Download_UnknownOrMismatchedAttachment_ThrowsNotFound()
    {
        var (db, user, applicationId) = await SeededApplicationAsync();
        var documentType = await SeedSupportingDocumentAsync(db, "HAS");
        await UploadHandler(db, user)
            .Handle(UploadCommand(applicationId, documentType), CancellationToken.None);
        var attachmentId = (await db.ApplicationAttachments.AsNoTracking().SingleAsync()).Id;
        var handler = new GetApplicationAttachmentDocumentHandler(db, user, _storage);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetApplicationAttachmentDocumentQuery(applicationId, Guid.NewGuid()),
            CancellationToken.None));
        // The Action cell of one application never streams another application's row.
        var foreignApplicationId = await SeedApplicationAsync(db, CompanyA);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetApplicationAttachmentDocumentQuery(foreignApplicationId, attachmentId),
            CancellationToken.None));
    }
}
