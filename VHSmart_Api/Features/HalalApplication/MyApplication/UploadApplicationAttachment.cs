using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// "Halal Application Supporting Document" upload of the Attachment side tab (spec 12.5):
// Document Type picked from the company's own Supporting Document rows with For View =
// Halal Application (R-06) plus the file. D-22 file rules (pdf/docx/xlsx/jpg/jpeg/png,
// 10 MB) are checked server side before anything is stored (BusinessRuleException -> 422).
// Bytes are stored first, the row second (CodingRules 10). A second upload for the same
// type REPLACES the row in place - Database.md 10 states no uniqueness rule, so no index
// enforces it (the Raw Material stance). D-26 keeps attachments allowed after submit (the
// read-only rule names them as the exception), so there is no EnsureDraft here. Only the
// owning company may upload: an unknown or foreign application answers 404.
public record UploadApplicationAttachmentCommand(
    Guid ApplicationId,
    Guid DocumentTypeId,
    IFormFile? File) : IRequest<IReadOnlyList<ApplicationAttachmentResponse>>;

public class UploadApplicationAttachmentValidator : AbstractValidator<UploadApplicationAttachmentCommand>
{
    public UploadApplicationAttachmentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.ApplicationId).NotEmpty();

        RuleFor(x => x.DocumentTypeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Document type is required.")
            .MustAsync((id, ct) => db.SupportingDocuments.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.ForView == SupportingDocumentForView.HalalApplication,
                ct))
            .WithMessage("Document type not found.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Document is required.");
    }
}

public class UploadApplicationAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadApplicationAttachmentHandler> logger)
    : IRequestHandler<UploadApplicationAttachmentCommand, IReadOnlyList<ApplicationAttachmentResponse>>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<IReadOnlyList<ApplicationAttachmentResponse>> Handle(
        UploadApplicationAttachmentCommand request,
        CancellationToken ct)
    {
        var application = await db.Applications
            .FirstOrDefaultAsync(
                row => row.Id == request.ApplicationId && row.CompanyId == user.CompanyId, ct);
        if (application is null)
            throw new NotFoundException("Application not found.");

        if (request.File is not { } file)
            throw new BusinessRuleException("Document is required.");

        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";
        var document = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = await db.ApplicationAttachments.FirstOrDefaultAsync(
            row => row.ApplicationId == application.Id && row.DocumentTypeId == request.DocumentTypeId,
            ct);

        StoredFile? previous = null;
        if (entity is null)
        {
            entity = new ApplicationAttachmentEntity
            {
                // The row's own company, not the caller's: the attachment belongs with the
                // application (the Raw Material upload stance).
                CompanyId = application.CompanyId,
                ApplicationId = application.Id,
                DocumentTypeId = request.DocumentTypeId,
                Document = document
            };
            db.ApplicationAttachments.Add(entity);
        }
        else
        {
            previous = entity.Document;
            entity.Document = document;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row was rejected: drop the bytes we just wrote. A storage failure here must
            // not hide the original save error, so it is only logged.
            try
            {
                await storage.DeleteAsync(document.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not remove orphaned application attachment {StorageKey}",
                    document.StorageKey);
            }

            throw;
        }

        // Replacement succeeded: the superseded file is no longer referenced.
        if (previous is not null)
        {
            try
            {
                await storage.DeleteAsync(previous.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not remove replaced application attachment {StorageKey}",
                    previous.StorageKey);
            }
        }

        return await ApplicationAttachmentListLoader.LoadAsync(db, application.Id, ct);
    }
}
