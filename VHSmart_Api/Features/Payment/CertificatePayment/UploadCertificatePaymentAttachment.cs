using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// "List of Attachments" upload (spec 15.1, Database.md 12): one stored file per row plus
// the typed DocumentType label* (no lookup source exists for it - SupportingDocumentForView
// holds no Payment value, so it is a required typed string, flagged). D-22 file rules
// (max 10 MB, pdf/docx/xlsx/jpg/jpeg/png) are enforced server-side; the bytes are stored
// before the row and dropped again if the save fails (the staff attachment pattern,
// CodingRules 10). COMPLETED rows are read-only (D-23) - 409. Unknown/foreign payment ->
// 404. Behind Payment.Certificate Edit (D-23 workflow stance - flagged vs the MyApplication
// upload's Create).
public record UploadCertificatePaymentAttachmentCommand(
    Guid Id,
    string DocumentType,
    IFormFile File) : IRequest<CertificatePaymentAttachmentResponse>;

public class UploadCertificatePaymentAttachmentValidator
    : AbstractValidator<UploadCertificatePaymentAttachmentCommand>
{
    public UploadCertificatePaymentAttachmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.DocumentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Document Type is required.")
            .MaximumLength(100)
            .WithMessage("Document Type cannot exceed 100 characters.");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required.");
    }
}

public class UploadCertificatePaymentAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadCertificatePaymentAttachmentHandler> logger)
    : IRequestHandler<
        UploadCertificatePaymentAttachmentCommand,
        CertificatePaymentAttachmentResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<CertificatePaymentAttachmentResponse> Handle(
        UploadCertificatePaymentAttachmentCommand request,
        CancellationToken ct)
    {
        var payment = await PaymentQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Certificate payment not found.");

        if (PaymentStatusPolicy.IsReadOnly(payment.Status))
        {
            throw new ConflictException("Completed payments cannot be edited.");
        }

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var document = await storage.SaveAsync(
            file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = new PaymentAttachmentEntity
        {
            CompanyId = user.CompanyId,
            PaymentId = payment.Id,
            DocumentType = request.DocumentType,
            Document = document
        };
        db.PaymentAttachments.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            try
            {
                await storage.DeleteAsync(document.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not remove orphaned payment attachment {StorageKey}",
                    document.StorageKey);
            }

            throw;
        }

        return new CertificatePaymentAttachmentResponse(
            entity.Id,
            entity.DocumentType,
            entity.Document.FileName,
            entity.Document.SizeBytes,
            entity.SysDateCreated);
    }
}
