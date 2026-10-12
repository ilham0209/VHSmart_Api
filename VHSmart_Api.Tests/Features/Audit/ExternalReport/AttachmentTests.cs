using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.Audit.ExternalReport.ExternalReportTestData;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class AttachmentTests
{
    private static UploadExternalAuditReportAttachmentHandler UploadHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        StubFileStorage? storage = null) =>
        new(db, user, storage ?? new StubFileStorage(),
            NullLogger<UploadExternalAuditReportAttachmentHandler>.Instance);

    private static IFormFile DocumentFile(
        string fileName = "report.pdf",
        byte[]? content = null,
        long? declaredLength = null)
    {
        var bytes = content ?? [37, 80, 68, 70];
        var file = new FormFile(
            new MemoryStream(bytes), 0, declaredLength ?? bytes.Length, "File", fileName);
        return file;
    }

    [Fact]
    public async Task Upload_Valid_StoresRowWithMetadata()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);

        var response = await UploadHandler(db, user).Handle(
            new UploadExternalAuditReportAttachmentCommand(
                reportId, DocumentFile("audit-report.pdf")),
            CancellationToken.None);

        Assert.Equal("audit-report.pdf", response.FileName);
        Assert.Equal(4, response.SizeBytes);
        var row = await db.ExternalAuditReportAttachments
            .SingleAsync(attachment => attachment.Id == response.Id);
        Assert.Equal(CompanyA, row.CompanyId);
        Assert.Equal(reportId, row.ExternalAuditReportId);
        Assert.Equal("application/pdf", row.Document.ContentType);
    }

    [Fact]
    public async Task Upload_UnknownReport_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            UploadHandler(db, user).Handle(
                new UploadExternalAuditReportAttachmentCommand(
                    Guid.NewGuid(), DocumentFile()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Upload_DisallowedExtension_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                new UploadExternalAuditReportAttachmentCommand(
                    reportId, DocumentFile("malware.exe")),
                CancellationToken.None));

        Assert.StartsWith("File type is not allowed.", exception.Message);
    }

    [Fact]
    public async Task Upload_OversizeFile_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UploadHandler(db, user).Handle(
                new UploadExternalAuditReportAttachmentCommand(
                    reportId, DocumentFile(declaredLength: 11 * 1024 * 1024)),
                CancellationToken.None));

        Assert.StartsWith("The file size exceeds the limit", exception.Message);
    }

    [Fact]
    public async Task Get_ListsOwnAttachments()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        await SeedAttachmentAsync(db, CompanyA, reportId, "first.pdf");
        await SeedAttachmentAsync(db, CompanyA, reportId, "second.pdf");

        var rows = await new GetExternalAuditReportAttachmentsHandler(db, user).Handle(
            new GetExternalAuditReportAttachmentsQuery(reportId), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.FileName == "first.pdf");
        Assert.Contains(rows, row => row.FileName == "second.pdf");
    }

    [Fact]
    public async Task Delete_Valid_SoftDeletes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, reportId);

        await new DeleteExternalAuditReportAttachmentHandler(db, user).Handle(
            new DeleteExternalAuditReportAttachmentCommand(attachmentId),
            CancellationToken.None);

        var row = await db.ExternalAuditReportAttachments.IgnoreQueryFilters()
            .SingleAsync(attachment => attachment.Id == attachmentId);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task Delete_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteExternalAuditReportAttachmentHandler(db, user).Handle(
                new DeleteExternalAuditReportAttachmentCommand(Guid.NewGuid()),
                CancellationToken.None));
    }
}
