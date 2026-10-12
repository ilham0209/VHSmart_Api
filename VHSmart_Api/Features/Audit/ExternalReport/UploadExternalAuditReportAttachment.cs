using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Tab "Attachment" upload (spec 14.10 tab, Database.md 13): one stored file per row.
// D-22 file rules (max 10 MB, pdf/docx/xlsx/jpg/jpeg/png) are enforced server-side; the
// bytes are stored before the row and dropped again if the save fails (the staff
// attachment pattern, CodingRules 10).
public record UploadExternalAuditReportAttachmentCommand(
    Guid Id,
    IFormFile File) : IRequest<ExternalAuditReportAttachmentResponse>;

public class UploadExternalAuditReportAttachmentValidator
    : AbstractValidator<UploadExternalAuditReportAttachmentCommand>
{
    public UploadExternalAuditReportAttachmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required.");
    }
}

public class UploadExternalAuditReportAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadExternalAuditReportAttachmentHandler> logger)
    : IRequestHandler<UploadExternalAuditReportAttachmentCommand, ExternalAuditReportAttachmentResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<ExternalAuditReportAttachmentResponse> Handle(
        UploadExternalAuditReportAttachmentCommand request,
        CancellationToken ct)
    {
        var reportOwned = await ExternalReportQueries.Own(db, user)
            .AsNoTracking()
            .AnyAsync(row => row.Id == request.Id, ct);
        if (!reportOwned)
        {
            throw new NotFoundException("External audit report not found.");
        }

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var document = await storage.SaveAsync(
            file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = new ExternalAuditReportAttachmentEntity
        {
            CompanyId = user.CompanyId,
            ExternalAuditReportId = request.Id,
            Document = document
        };
        db.ExternalAuditReportAttachments.Add(entity);

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
                    "Could not remove orphaned external report attachment {StorageKey}",
                    document.StorageKey);
            }

            throw;
        }

        return new ExternalAuditReportAttachmentResponse(
            entity.Id, entity.Document.FileName, entity.Document.SizeBytes);
    }
}
