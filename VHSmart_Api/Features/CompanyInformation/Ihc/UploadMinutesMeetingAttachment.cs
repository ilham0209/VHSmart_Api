using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.CompanyInformation.Ihc;

// "Attachment" tab of the meeting form (spec 7.6: Upload Document + List of Attachments).
// The meeting id comes from the route, the company from the JWT. Database.md 4 marks no
// asterisk on Document, so a row may be stored without bytes - the download answers 404
// until one is uploaded (the same reading the training modules got). File rules follow
// D-22's general list; bytes are stored before the row (CodingRules 10).
public record UploadMinutesMeetingAttachmentCommand(
    Guid MinutesMeetingId,
    IFormFile? File) : IRequest<MinutesMeetingDetailResponse>;

public class UploadMinutesMeetingAttachmentValidator
    : AbstractValidator<UploadMinutesMeetingAttachmentCommand>
{
    public UploadMinutesMeetingAttachmentValidator()
    {
        RuleFor(x => x.MinutesMeetingId).NotEmpty();
    }
}

public class UploadMinutesMeetingAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadMinutesMeetingAttachmentHandler> logger)
    : IRequestHandler<UploadMinutesMeetingAttachmentCommand, MinutesMeetingDetailResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<MinutesMeetingDetailResponse> Handle(
        UploadMinutesMeetingAttachmentCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var owned = await db.MinutesMeetings.AsNoTracking()
            .AnyAsync(row => row.Id == request.MinutesMeetingId && row.CompanyId == companyId, ct);
        if (!owned)
            throw new NotFoundException("Minutes meeting not found.");

        StoredFile? document = null;
        if (request.File is not null)
        {
            var file = request.File;
            FileValidation.Validate(file.FileName, file.Length);

            var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
                ? mapped
                : "application/octet-stream";

            document = await storage.SaveAsync(
                file.OpenReadStream(), file.FileName, contentType, ct);
        }

        var entity = new MinutesMeetingAttachmentEntity
        {
            CompanyId = companyId,
            MinutesMeetingId = request.MinutesMeetingId,
            Document = document
        };
        db.MinutesMeetingAttachments.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row was rejected: drop the bytes we just wrote (if any). A storage failure
            // here must not hide the original save error, so it is only logged.
            if (document is not null)
            {
                try
                {
                    await storage.DeleteAsync(document.StorageKey, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Could not remove orphaned minutes meeting attachment {StorageKey}",
                        document.StorageKey);
                }
            }

            throw;
        }

        return await MinutesMeetingResponseLoader.LoadAsync(
            db, companyId, request.MinutesMeetingId, ct);
    }
}
