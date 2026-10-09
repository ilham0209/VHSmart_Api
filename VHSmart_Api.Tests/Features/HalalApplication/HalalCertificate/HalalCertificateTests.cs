using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.HalalApplication.HalalCertificate;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using static VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.HalalCertificate;

// Spec 12.8 "List of Halal Certificate" + View/Edit detail: the item count, the derived
// Valid / Expired status (D-04 over the shared Asia/Kuala_Lumpur clock), the Database.md
// edit columns (flagged: the legacy dialog's field list was never captured), the D-22
// upload rules and the streamed download.
public class HalalCertificateTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartHalalCertificateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public HalalCertificateTests() => _storage = new LocalFileStorage(_storageRoot);

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static IFormFile DocumentFile(string fileName = "certificate.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    private UploadHalalCertificateDocumentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<UploadHalalCertificateDocumentHandler>.Instance);

    private static async Task<
        (TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId, Guid certificateId)>
        SeededCertificateAsync(
            string certificateNo = "HAL-2026-0001",
            DateOnly? expiryDate = null,
            StoredFile? document = null,
            Guid? batchId = null)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            referenceNo: "VHS(PR)/01012026/1",
            status: ApplicationStatus.ApplicationApproved,
            batchId: batchId);
        var certificateId = await SeedCertificateAsync(
            db, CompanyA, applicationId, certificateNo,
            expiryDate: expiryDate, document: document);
        return (db, user, applicationId, certificateId);
    }

    [Fact]
    public async Task List_ReturnsTheCountSchemeReferenceAndCbReference()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await BatchTestData.SeedBatchAsync(
            db, CompanyA, cbReferenceNo: "CB-REF-9");
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            referenceNo: "VHS(PR)/01012026/1",
            status: ApplicationStatus.ApplicationApproved,
            batchId: batchId);
        var certificateId = await SeedCertificateAsync(
            db, CompanyA, applicationId, "HAL-2026-0001");
        await SeedCertificateItemAsync(
            db, CompanyA, applicationId, "Item 1", certificateId: certificateId);
        await SeedCertificateItemAsync(
            db, CompanyA, applicationId, "Item 2", certificateId: certificateId);

        var response = await new GetHalalCertificatesHandler(db, user)
            .Handle(new GetHalalCertificatesQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal("HAL-2026-0001", row.CertificateNumber);
        Assert.Equal("VHS(PR)/01012026/1", row.ReferenceNo);
        Assert.Equal("CB-REF-9", row.CbReferenceNo);
        Assert.Equal(2, row.NoCertificateItem);
        // The application's scheme name (the seeded product scheme), never empty.
        Assert.False(string.IsNullOrWhiteSpace(row.Scheme));
    }

    [Fact]
    public async Task List_DerivesValidAndExpiredPerD04()
    {
        var (db, user, applicationId, _) = await SeededCertificateAsync();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await SeedCertificateAsync(db, CompanyA, applicationId, "EXP-1", expiryDate: past);
        await SeedCertificateAsync(db, CompanyA, applicationId, "FUT-1", expiryDate: future);
        await SeedCertificateAsync(db, CompanyA, applicationId, "NONE-1");
        await SeedCertificateAsync(
            db, CompanyA, applicationId, "SENT-1",
            expiryDate: HalalStatusCalculator.NoExpiryDate);

        var response = await new GetHalalCertificatesHandler(db, user)
            .Handle(new GetHalalCertificatesQuery(), CancellationToken.None);

        Assert.Equal(5, response.Data.Count());
        var expired = response.Data.Single(row => row.CertificateNumber == "EXP-1");
        Assert.Equal("Expired", expired.HalalCertificateStatus);
        Assert.Equal(past, expired.ExpiryDate);

        var valid = response.Data.Single(row => row.CertificateNumber == "FUT-1");
        Assert.Equal("Valid", valid.HalalCertificateStatus);
        Assert.Equal(future, valid.ExpiryDate);

        var untouched = response.Data.Single(row => row.CertificateNumber == "NONE-1");
        Assert.Equal("Valid", untouched.HalalCertificateStatus);
        Assert.Null(untouched.ExpiryDate);

        var sentinel = response.Data.Single(row => row.CertificateNumber == "SENT-1");
        Assert.Equal("Valid", sentinel.HalalCertificateStatus);
        // D-04: the sentinel shows empty, never as year 9999.
        Assert.Null(sentinel.ExpiryDate);
    }

    [Fact]
    public async Task List_ForeignCompanyRows_AreHidden()
    {
        var (db, user, _, _) = await SeededCertificateAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        await SeedCertificateAsync(db, CompanyB, foreignApplicationId, "FOR-1");

        var response = await new GetHalalCertificatesHandler(db, user)
            .Handle(new GetHalalCertificatesQuery(), CancellationToken.None);

        Assert.Equal("HAL-2026-0001", Assert.Single(response.Data).CertificateNumber);
    }

    [Fact]
    public async Task GetById_ReturnsTheDetailWithCoveredItemsAndFileName()
    {
        var document = new StoredFile
        {
            FileName = "certificate.pdf",
            StorageKey = "key-existing",
            ContentType = "application/pdf",
            SizeBytes = 4
        };
        var (db, user, applicationId, certificateId) =
            await SeededCertificateAsync(document: document);
        await SeedCertificateItemAsync(
            db, CompanyA, applicationId, "Zeta Item", certificateId: certificateId);
        await SeedCertificateItemAsync(
            db, CompanyA, applicationId, "Alpha Item", certificateId: certificateId);

        var response = await new GetHalalCertificateByIdHandler(db, user)
            .Handle(new GetHalalCertificateByIdQuery(certificateId), CancellationToken.None);

        Assert.Equal("HAL-2026-0001", response.CertificateNumber);
        Assert.Equal(applicationId, response.ApplicationId);
        Assert.Equal("certificate.pdf", response.FileName);
        Assert.Equal(
            new[] { "Alpha Item", "Zeta Item" },
            response.Items.Select(item => item.CertificateItem).ToArray());
    }

    [Fact]
    public async Task GetById_WithoutDocument_ShowsNullFileNameAndEmptyExpiry()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync(
            expiryDate: HalalStatusCalculator.NoExpiryDate);

        var response = await new GetHalalCertificateByIdHandler(db, user)
            .Handle(new GetHalalCertificateByIdQuery(certificateId), CancellationToken.None);

        Assert.Null(response.FileName);
        Assert.Null(response.ExpiryDate);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task GetById_UnknownOrForeign_ThrowsNotFound()
    {
        var (db, user, _, _) = await SeededCertificateAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        var foreignCertificateId = await SeedCertificateAsync(
            db, CompanyB, foreignApplicationId, "FOR-1");
        var handler = new GetHalalCertificateByIdHandler(db, user);

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetHalalCertificateByIdQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal("Halal certificate not found.", unknown.Message);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetHalalCertificateByIdQuery(foreignCertificateId), CancellationToken.None));
    }

    [Fact]
    public async Task Update_ChangesTheNumberAndDates()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();
        var issued = new DateOnly(2026, 1, 15);
        var expiry = new DateOnly(2027, 1, 14);

        var response = await new UpdateHalalCertificateHandler(db, user)
            .Handle(
                new UpdateHalalCertificateCommand(certificateId, "  HAL-NEW-9  ", issued, expiry),
                CancellationToken.None);

        Assert.Equal("HAL-NEW-9", response.CertificateNumber);
        Assert.Equal(issued, response.IssuedDate);
        Assert.Equal(expiry, response.ExpiryDate);

        var stored = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.Equal("HAL-NEW-9", stored.CertificateNo);
        Assert.Equal(issued, stored.IssuedDate);
        Assert.Equal(expiry, stored.ExpiryDate);
    }

    [Fact]
    public async Task Update_ANumberAnotherRowOwns_ThrowsConflict()
    {
        var (db, user, applicationId, _) = await SeededCertificateAsync(certificateNo: "HAL-1");
        var secondId = await SeedCertificateAsync(
            db, CompanyA, applicationId, certificateNo: "HAL-2");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateHalalCertificateHandler(db, user).Handle(
                new UpdateHalalCertificateCommand(secondId, "hal-1", null, null),
                CancellationToken.None));

        Assert.Equal("Certificate number already exists.", exception.Message);
        Assert.Equal(
            "HAL-2",
            (await db.HalalCertificates.AsNoTracking()
                .SingleAsync(row => row.Id == secondId)).CertificateNo);
    }

    [Fact]
    public async Task Update_UnknownOrForeign_ThrowsNotFound()
    {
        var (db, user, _, _) = await SeededCertificateAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        var foreignCertificateId = await SeedCertificateAsync(
            db, CompanyB, foreignApplicationId, "FOR-1");
        var handler = new UpdateHalalCertificateHandler(db, user);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateHalalCertificateCommand(Guid.NewGuid(), "HAL-1", null, null),
            CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateHalalCertificateCommand(foreignCertificateId, "HAL-1", null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_ValidFile_StoresTheDocumentAndTheBytes()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();

        var response = await UploadHandler(db, user)
            .Handle(
                new UploadHalalCertificateDocumentCommand(certificateId, DocumentFile()),
                CancellationToken.None);

        Assert.Equal("certificate.pdf", response.FileName);

        var stored = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.NotNull(stored.Document);
        Assert.Equal("certificate.pdf", stored.Document.FileName);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document.StorageKey)));
    }

    [Fact]
    public async Task Upload_SecondFile_ReplacesTheFirstAndDropsItsBytes()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();
        await UploadHandler(db, user)
            .Handle(
                new UploadHalalCertificateDocumentCommand(certificateId, DocumentFile()),
                CancellationToken.None);
        var uploaded = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.NotNull(uploaded.Document);
        var firstKey = uploaded.Document.StorageKey;

        await UploadHandler(db, user)
            .Handle(
                new UploadHalalCertificateDocumentCommand(
                    certificateId, DocumentFile("second.pdf")),
                CancellationToken.None);

        var stored = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.NotNull(stored.Document);
        Assert.Equal("second.pdf", stored.Document.FileName);
        Assert.False(File.Exists(Path.Combine(_storageRoot, firstKey)));
        Assert.Single(Directory.GetFiles(_storageRoot));
    }

    [Fact]
    public async Task Upload_DisallowedExtension_ThrowsTheD22Rule()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                new UploadHalalCertificateDocumentCommand(
                    certificateId, DocumentFile("payload.exe")),
                CancellationToken.None));

        Assert.StartsWith("File type is not allowed.", exception.Message);
        var stored = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.Null(stored.Document);
    }

    [Fact]
    public async Task Upload_WithoutFile_ThrowsTheRequiredRule()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                new UploadHalalCertificateDocumentCommand(certificateId, null),
                CancellationToken.None));

        Assert.Equal("Document is required.", exception.Message);
    }

    [Fact]
    public async Task Upload_UnknownOrForeign_ThrowsNotFound()
    {
        var (db, user, _, _) = await SeededCertificateAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        var foreignCertificateId = await SeedCertificateAsync(
            db, CompanyB, foreignApplicationId, "FOR-1");
        var handler = UploadHandler(db, user);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UploadHalalCertificateDocumentCommand(Guid.NewGuid(), DocumentFile()),
            CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UploadHalalCertificateDocumentCommand(foreignCertificateId, DocumentFile()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Download_StreamsTheUploadedFile()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();
        await UploadHandler(db, user)
            .Handle(
                new UploadHalalCertificateDocumentCommand(certificateId, DocumentFile()),
                CancellationToken.None);

        var response = await new GetHalalCertificateDocumentHandler(db, user, _storage)
            .Handle(new GetHalalCertificateDocumentQuery(certificateId), CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Download_WithoutADocument_ThrowsNoDocument()
    {
        var (db, user, _, certificateId) = await SeededCertificateAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetHalalCertificateDocumentHandler(db, user, _storage)
                .Handle(new GetHalalCertificateDocumentQuery(certificateId), CancellationToken.None));

        Assert.Equal("No document.", exception.Message);
    }

    [Fact]
    public async Task Download_UnknownOrForeign_ThrowsNotFound()
    {
        var (db, user, _, _) = await SeededCertificateAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        var foreignCertificateId = await SeedCertificateAsync(
            db, CompanyB, foreignApplicationId, "FOR-1");
        var handler = new GetHalalCertificateDocumentHandler(db, user, _storage);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetHalalCertificateDocumentQuery(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetHalalCertificateDocumentQuery(foreignCertificateId), CancellationToken.None));
    }

    [Fact]
    public async Task Validator_EmptyNumberAndMissingFile_FailTheMessages()
    {
        var certificateValidator = new UpdateHalalCertificateValidator();
        var numberResult = await certificateValidator.ValidateAsync(
            new UpdateHalalCertificateCommand(Guid.NewGuid(), "", null, null));
        Assert.False(numberResult.IsValid);
        Assert.Contains(numberResult.Errors,
            failure => failure.ErrorMessage == "Certificate number is required.");

        var lengthResult = await certificateValidator.ValidateAsync(
            new UpdateHalalCertificateCommand(Guid.NewGuid(), new string('x', 101), null, null));
        Assert.False(lengthResult.IsValid);
        Assert.Contains(
            lengthResult.Errors,
            failure => failure.ErrorMessage
                == "Certificate number must be 100 characters or fewer.");

        var uploadValidator = new UploadHalalCertificateDocumentValidator();
        var fileResult = await uploadValidator.ValidateAsync(
            new UploadHalalCertificateDocumentCommand(Guid.NewGuid(), null));
        Assert.False(fileResult.IsValid);
        Assert.Contains(fileResult.Errors,
            failure => failure.ErrorMessage == "Document is required.");
    }
}
