using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VHSmart_Api.Features.Payment.CertificatePayment;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;
using static VHSmart_Api.Tests.Features.Payment.CertificatePayment.CertificatePaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

public class CertificatePaymentAttachmentTests
{
    private static IFormFile PdfFile(string fileName = "charge-letter.pdf", long length = 128) =>
        new FormFile(new MemoryStream(new byte[length]), 0, length, "File", fileName);

    private static UploadCertificatePaymentAttachmentCommand UploadCommand(
        Guid paymentId,
        string documentType = "Charge Letter",
        IFormFile? file = null) => new(
        paymentId, documentType, file ?? PdfFile());

    [Fact]
    public async Task Upload_SavesRowWithDocumentType()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var response = await UploadAsync(db, user, paymentId);

        Assert.Equal("Charge Letter", response.DocumentType);
        Assert.Equal("charge-letter.pdf", response.FileName);
        Assert.Equal(128, response.SizeBytes);
        Assert.True(await db.PaymentAttachments.AnyAsync(row => row.Id == response.Id));
    }

    private static async Task<CertificatePaymentAttachmentResponse> UploadAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Guid paymentId,
        string documentType = "Charge Letter",
        IFormFile? file = null) =>
        await new UploadCertificatePaymentAttachmentHandler(
                db, user, new StubStorage(), new NullLogger())
            .Handle(UploadCommand(paymentId, documentType, file), CancellationToken.None);

    [Fact]
    public async Task UploadValidator_MissingDocumentType_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var result = await new UploadCertificatePaymentAttachmentValidator()
            .ValidateAsync(UploadCommand(paymentId, documentType: string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Document Type is required.");
    }

    [Fact]
    public async Task Upload_DocumentTypeTooLong_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var result = await new UploadCertificatePaymentAttachmentValidator()
            .ValidateAsync(UploadCommand(paymentId, documentType: new string('x', 101)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Document Type cannot exceed 100 characters.");
    }

    [Fact]
    public async Task Upload_BadExtension_IsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadAsync(db, user, paymentId, file: PdfFile("virus.exe")));

        Assert.StartsWith("File type is not allowed.", exception.Message);
    }

    [Fact]
    public async Task Upload_Oversized_IsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadAsync(db, user, paymentId, file: PdfFile(length: 11 * 1024 * 1024)));

        Assert.Equal("The file size exceeds the limit of 10 MB.", exception.Message);
    }

    [Fact]
    public async Task Upload_UnknownPayment_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadAsync(db, user, Guid.NewGuid()));
    }

    [Fact]
    public async Task Upload_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA, PaymentStatus.Completed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            UploadAsync(db, user, paymentId));

        Assert.Equal("Completed payments cannot be edited.", exception.Message);
    }

    [Fact]
    public async Task GetAttachments_ReturnsUploadInOrder()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var first = await UploadAsync(db, user, paymentId, documentType: "Charge Letter");
        var second = await UploadAsync(db, user, paymentId, documentType: "Bank Draft");

        var list = await new GetCertificatePaymentAttachmentsHandler(db, user).Handle(
            new GetCertificatePaymentAttachmentsQuery(paymentId), CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal([first.Id, second.Id], list.Select(row => row.Id));
        Assert.Equal(["Charge Letter", "Bank Draft"], list.Select(row => row.DocumentType));
    }

    [Fact]
    public async Task GetAttachments_UnknownPayment_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificatePaymentAttachmentsHandler(db, user).Handle(
                new GetCertificatePaymentAttachmentsQuery(Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAttachment_SoftDeletesRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var uploaded = await UploadAsync(db, user, paymentId);

        await new DeleteCertificatePaymentAttachmentHandler(db, user).Handle(
            new DeleteCertificatePaymentAttachmentCommand(paymentId, uploaded.Id),
            CancellationToken.None);

        var attachment = await db.PaymentAttachments.IgnoreQueryFilters()
            .FirstAsync(row => row.Id == uploaded.Id);
        Assert.True(attachment.IsDeleted);
    }

    [Fact]
    public async Task DeleteAttachment_MismatchedPayment_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var uploaded = await UploadAsync(db, user, paymentId);
        var otherApplication = await SeedApplicationAsync(db, CompanyA, await SeedSchemeAsync(db));
        var staffId = await SeedStaffAsync(db, CompanyA);
        var otherPayment = await SeedPaymentAsync(
            db, CompanyA, otherApplication, staffId, staffId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteCertificatePaymentAttachmentHandler(db, user).Handle(
                new DeleteCertificatePaymentAttachmentCommand(otherPayment, uploaded.Id),
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAttachment_Completed_IsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA, PaymentStatus.Completed);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, paymentId);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteCertificatePaymentAttachmentHandler(db, user).Handle(
                new DeleteCertificatePaymentAttachmentCommand(paymentId, attachmentId),
                CancellationToken.None));

        Assert.Equal("Completed payments cannot be edited.", exception.Message);
    }

    [Fact]
    public async Task DeleteAttachment_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteCertificatePaymentAttachmentHandler(db, user).Handle(
                new DeleteCertificatePaymentAttachmentCommand(paymentId, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Download_ReturnsStoredStream()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, paymentId);
        var storage = new StubStorage();

        var response = await new GetCertificatePaymentAttachmentDocumentHandler(db, user, storage)
            .Handle(new GetCertificatePaymentAttachmentDocumentQuery(paymentId, attachmentId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        Assert.NotNull(response.Content);
    }

    [Fact]
    public async Task Download_Unknown_IsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCertificatePaymentAttachmentDocumentHandler(db, user, new StubStorage())
                .Handle(new GetCertificatePaymentAttachmentDocumentQuery(
                    paymentId, Guid.NewGuid()), CancellationToken.None));
    }

    // Minimal storage + logger doubles: the upload handler only needs SaveAsync; the download
    // handler only OpenReadAsync; the orphan-cleanup path never fires here.
    private sealed class StubStorage : IFileStorage
    {
        public Task<StoredFile> SaveAsync(
            Stream content, string fileName, string contentType, CancellationToken ct) =>
            Task.FromResult(new StoredFile
            {
                FileName = fileName,
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = contentType,
                SizeBytes = 128
            });

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));

        public Task DeleteAsync(string storageKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullLogger : ILogger<UploadCertificatePaymentAttachmentHandler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
