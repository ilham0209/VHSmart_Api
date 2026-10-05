using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using static VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

// Section "Attachment Information" of spec 10.2 and the Halal Information column it feeds:
// one row per the company's Raw Material document type (R-06) with an N/A placeholder until
// uploaded, per-row Valid / Expired from the expiry date (D-04), the D-22 upload rules,
// replace-in-place per type and the streamed download.
public class RawMaterialAttachmentTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartRawMaterialAttachmentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public RawMaterialAttachmentTests() => _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IFormFile DocumentFile(string fileName = "halal-cert.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    private static UploadRawMaterialAttachmentCommand UploadCommand(
        Guid rawMaterialId,
        Guid documentTypeId,
        DateTime? expiryDate = null,
        string? referenceNo = null,
        string? authority = null,
        IFormFile? file = null) =>
        new(rawMaterialId, documentTypeId, expiryDate, referenceNo, authority, file ?? DocumentFile());

    private UploadRawMaterialAttachmentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadRawMaterialAttachmentHandler>.Instance);

    [Fact]
    public async Task List_ReturnsEveryDocumentTypeInOrder_WithNarowsForTheUntouchedOnes()
    {
        var db = await CreateDbAsync(UserA());
        var rawMaterialId = await SeedRowAsync(db);
        var specification = await SeedSupportingDocumentAsync(
            db, "PRODUCT SPECIFICATION", documentSequence: 2);
        var certificate = await SeedSupportingDocumentAsync(
            db, "HALAL CERTIFICATE", documentSequence: 1);
        await SeedAttachmentAsync(
            db, rawMaterialId, certificate,
            expiryDate: new DateTime(2099, 6, 1), referenceNo: "HC-001", authority: "JAKIM");

        var response = await new GetRawMaterialAttachmentsHandler(db)
            .Handle(new GetRawMaterialAttachmentsQuery(rawMaterialId), CancellationToken.None);

        // The section is driven by the document-type list, in document-sequence order, not by
        // the rows that happen to exist (spec 10.2: "N/A shown until uploaded").
        Assert.Equal(
            new[] { "HALAL CERTIFICATE", "PRODUCT SPECIFICATION" },
            response.Select(row => row.DocumentType).ToArray());

        var uploaded = response[0];
        Assert.NotNull(uploaded.Id);
        Assert.Equal(certificate, uploaded.DocumentTypeId);
        Assert.Equal("certificate.pdf", uploaded.FileName);
        Assert.Equal(new DateOnly(2099, 6, 1), uploaded.ExpiryDate);
        Assert.Equal("HC-001", uploaded.ReferenceNo);
        Assert.Equal("JAKIM", uploaded.Authority);
        Assert.Equal(HalalStatus.Valid, uploaded.DocumentStatus);

        var untouched = response[1];
        Assert.Null(untouched.Id);
        Assert.Equal(specification, untouched.DocumentTypeId);
        Assert.Null(untouched.FileName);
        Assert.Null(untouched.ExpiryDate);
        Assert.Null(untouched.DocumentStatus);
    }

    [Fact]
    public async Task List_ExpiredAndSentinelDates_KeepTheD04Rules()
    {
        var db = await CreateDbAsync(UserA());
        var rawMaterialId = await SeedRowAsync(db);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        var processFlow = await SeedSupportingDocumentAsync(db, "PROCESS FLOW", documentSequence: 2);
        await SeedAttachmentAsync(
            db, rawMaterialId, certificate, expiryDate: new DateTime(2020, 1, 1));
        await SeedAttachmentAsync(
            db, rawMaterialId, processFlow, expiryDate: new DateTime(9999, 12, 31));

        var response = await new GetRawMaterialAttachmentsHandler(db)
            .Handle(new GetRawMaterialAttachmentsQuery(rawMaterialId), CancellationToken.None);

        var expired = response.Single(row => row.DocumentType == "HALAL CERTIFICATE");
        Assert.Equal(HalalStatus.Expired, expired.DocumentStatus);
        Assert.Equal(new DateOnly(2020, 1, 1), expired.ExpiryDate);

        // D-04: the sentinel means "no expiry" - shown empty and never expired.
        var sentinel = response.Single(row => row.DocumentType == "PROCESS FLOW");
        Assert.Equal(HalalStatus.Valid, sentinel.DocumentStatus);
        Assert.Null(sentinel.ExpiryDate);
    }

    [Fact]
    public async Task List_UnknownOrForeignRawMaterial_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);
        var handler = new GetRawMaterialAttachmentsHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetRawMaterialAttachmentsQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetRawMaterialAttachmentsQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task List_RowSharedWithTheCaller_SeesTheirOwnTypesAndNoForeignUploads()
    {
        var db = await CreateDbAsync(UserB());
        var sharedId = await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);

        // The owner's certificate is invisible to the sharing company: the section reads the
        // tenant-scoped sets, exactly like the manufacturer and status joins of the list.
        var ownerType = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE", CompanyA);
        await SeedAttachmentAsync(db, sharedId, ownerType, companyId: CompanyA, referenceNo: "OWNER");

        var withoutTypes = await new GetRawMaterialAttachmentsHandler(db)
            .Handle(new GetRawMaterialAttachmentsQuery(sharedId), CancellationToken.None);
        Assert.Empty(withoutTypes);

        await SeedSupportingDocumentAsync(db, "PROCESS FLOW", CompanyB);
        var withTypes = await new GetRawMaterialAttachmentsHandler(db)
            .Handle(new GetRawMaterialAttachmentsQuery(sharedId), CancellationToken.None);

        var only = Assert.Single(withTypes);
        Assert.Equal("PROCESS FLOW", only.DocumentType);
        Assert.Null(only.Id);
    }

    [Fact]
    public async Task Upload_NewType_StoresRowAndBytesAndReturnsTheSection()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");

        var response = await UploadHandler(db, user).Handle(
            UploadCommand(rawMaterialId, certificate,
                new DateTime(2099, 6, 1), "HC-001", "JAKIM"),
            CancellationToken.None);

        var uploaded = Assert.Single(response);
        Assert.Equal("HALAL CERTIFICATE", uploaded.DocumentType);
        Assert.NotNull(uploaded.Id);
        Assert.Equal("halal-cert.pdf", uploaded.FileName);
        Assert.Equal(HalalStatus.Valid, uploaded.DocumentStatus);

        var stored = await db.RawMaterialAttachments.AsNoTracking().SingleAsync();
        Assert.Equal(CompanyA, stored.CompanyId);
        Assert.Equal(rawMaterialId, stored.RawMaterialId);
        Assert.Equal(certificate, stored.DocumentTypeId);
        Assert.Equal("JAKIM", stored.Authority);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        // The snapshot column of Database.md 8 is written, screens recompute it (D-04).
        Assert.Equal("Valid", stored.DocumentStatus);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
    }

    [Fact]
    public async Task Upload_ExistingType_ReplacesTheRowAndTheBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");

        await UploadHandler(db, user).Handle(
            UploadCommand(rawMaterialId, certificate, new DateTime(2099, 6, 1), "FIRST"),
            CancellationToken.None);
        var firstKey = (await db.RawMaterialAttachments.SingleAsync()).Document.StorageKey;

        var response = await UploadHandler(db, user).Handle(
            UploadCommand(rawMaterialId, certificate, new DateTime(2020, 1, 1), "SECOND",
                file: DocumentFile("halal-v2.pdf")),
            CancellationToken.None);

        // One row per type in the section: the second upload replaced the first file instead
        // of adding a second row for the same document type.
        Assert.Equal(1, await db.RawMaterialAttachments.CountAsync());
        var stored = await db.RawMaterialAttachments.SingleAsync();
        Assert.Equal("halal-v2.pdf", stored.Document.FileName);
        Assert.Equal(new DateTime(2020, 1, 1), stored.ExpiryDate);
        Assert.Equal("SECOND", stored.ReferenceNo);
        Assert.Equal("Expired", stored.DocumentStatus);
        Assert.False(File.Exists(Path.Combine(_storageRoot, firstKey)));
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));

        var row = Assert.Single(response);
        Assert.Equal("halal-v2.pdf", row.FileName);
        Assert.Equal(HalalStatus.Expired, row.DocumentStatus);
    }

    [Fact]
    public async Task Upload_UnknownOrForeignRawMaterial_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignId = await SeedRowAsync(db, companyId: CompanyB);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        var handler = UploadHandler(db, UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(UploadCommand(Guid.NewGuid(), certificate), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(UploadCommand(foreignId, certificate), CancellationToken.None));

        Assert.Equal(0, await db.RawMaterialAttachments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Upload_RowSharedWithTheCaller_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserB());
        var sharedId = await SeedRowAsync(
            db,
            companyId: CompanyA,
            accessibleCompanyIds: [await SeedCompanyAsync(db, "Company B", CompanyB)]);
        var certificate = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE", CompanyB);

        // Sharing is read-only (CodingRules 7.3): the row answers the read (the list test
        // above proves it) but never the upload.
        var section = await new GetRawMaterialAttachmentsHandler(db)
            .Handle(new GetRawMaterialAttachmentsQuery(sharedId), CancellationToken.None);
        var only = Assert.Single(section);
        Assert.Equal("HALAL CERTIFICATE", only.DocumentType);
        Assert.Null(only.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, UserB()).Handle(
                UploadCommand(sharedId, certificate), CancellationToken.None));

        Assert.Equal(0, await db.RawMaterialAttachments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Validator_DocumentTypeMustBeTheCallersOwnRawMaterialType_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var validator = new UploadRawMaterialAttachmentValidator(db, user);
        var ownType = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        var otherView = await SeedSupportingDocumentAsync(
            db, "Staff contract", forView: SupportingDocumentForView.AllStaff);
        var foreignType = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE", CompanyB);

        foreach (var typeId in new[] { Guid.NewGuid(), otherView, foreignType })
        {
            var result = await validator.ValidateAsync(
                UploadCommand(rawMaterialId, typeId));

            Assert.False(result.IsValid);
            Assert.Contains(
                result.Errors,
                failure => failure.PropertyName == "DocumentTypeId"
                    && failure.ErrorMessage == "Document type not found.");
        }

        Assert.True((await validator.ValidateAsync(UploadCommand(rawMaterialId, ownType))).IsValid);
    }

    [Fact]
    public async Task Validator_MissingFileOrTooLongFields_Fail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var type = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        var validator = new UploadRawMaterialAttachmentValidator(db, user);

        var missingFile = await validator.ValidateAsync(
            new UploadRawMaterialAttachmentCommand(rawMaterialId, type, null, null, null, null));
        Assert.False(missingFile.IsValid);
        Assert.Contains(
            missingFile.Errors,
            failure => failure.ErrorMessage == "Document is required.");

        var tooLong = await validator.ValidateAsync(
            UploadCommand(rawMaterialId, type,
                referenceNo: new string('a', 101), authority: new string('b', 201)));
        Assert.False(tooLong.IsValid);
        Assert.Contains(
            tooLong.Errors,
            failure => failure.ErrorMessage == "Reference number must be 100 characters or fewer.");
        Assert.Contains(
            tooLong.Errors,
            failure => failure.ErrorMessage == "Authority must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Upload_DisallowedFileType_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var type = await SeedSupportingDocumentAsync(db, "PROCESS FLOW");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(rawMaterialId, type, file: DocumentFile("process.txt")),
                CancellationToken.None));

        // Rejected before anything was stored (D-22).
        Assert.Equal(0, await db.RawMaterialAttachments.IgnoreQueryFilters().CountAsync());
        Assert.False(Directory.Exists(_storageRoot) && Directory.EnumerateFiles(_storageRoot).Any());
    }

    [Fact]
    public async Task Upload_OversizeFile_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var type = await SeedSupportingDocumentAsync(db, "PROCESS FLOW");
        var oversize = new FormFile(
            new MemoryStream([1]), 0, (10 * 1024 * 1024) + 1, "File", "huge.pdf");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                UploadCommand(rawMaterialId, type, file: oversize),
                CancellationToken.None));

        Assert.Equal(0, await db.RawMaterialAttachments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Document_StreamsTheStoredFile()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var type = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        await UploadHandler(db, user).Handle(
            UploadCommand(rawMaterialId, type), CancellationToken.None);
        var attachmentId = (await db.RawMaterialAttachments.SingleAsync()).Id;

        var response = await new GetRawMaterialAttachmentDocumentHandler(db, user, _storage)
            .Handle(new GetRawMaterialAttachmentDocumentQuery(rawMaterialId, attachmentId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Document_UnknownMismatchedOrForeignId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var rawMaterialId = await SeedRowAsync(db);
        var type = await SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE");
        var foreignRawMaterialId = await SeedRowAsync(
            db, ingredient: "Corn Flour", ingredientCode: "RM-002", companyId: CompanyB);
        var foreignAttachmentId = await SeedAttachmentAsync(
            db, foreignRawMaterialId, await SeedSupportingDocumentAsync(
                db, "HALAL CERTIFICATE", CompanyB), companyId: CompanyB);
        await UploadHandler(db, user).Handle(
            UploadCommand(rawMaterialId, type), CancellationToken.None);
        var attachmentId = (await db.RawMaterialAttachments.SingleAsync()).Id;
        var handler = new GetRawMaterialAttachmentDocumentHandler(db, user, _storage);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetRawMaterialAttachmentDocumentQuery(rawMaterialId, Guid.NewGuid()),
                CancellationToken.None));

        // The attachment must belong to the raw material the route names.
        var otherRawMaterialId = await SeedRowAsync(
            db, ingredient: "Barley", ingredientCode: "RM-003");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetRawMaterialAttachmentDocumentQuery(otherRawMaterialId, attachmentId),
                CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetRawMaterialAttachmentDocumentQuery(foreignRawMaterialId, foreignAttachmentId),
                CancellationToken.None));
    }
}
