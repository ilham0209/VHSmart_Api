using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.HalalApplication.HalalCertificate;

// Step 5 of the manual flow (spec 12.8): Click Upload, choose the file - the bytes land on
// the certificate's Document File column (Database.md 10 AppHalalCertificates). D-22 file
// rules (pdf/docx/xlsx/jpg/jpeg/png, 10 MB) are checked server side before anything is
// stored (BusinessRuleException -> 422); a second upload REPLACES the document in place and
// drops the superseded bytes, the HA-04 attachment stance. Bytes are stored first, the row
// second (CodingRules 10) with orphan cleanup on a failed save. D-26 keeps certificate work
// allowed after submit, so there is no EnsureDraft. Unknown or foreign certificates answer
// 404; identity comes from the JWT.
public record UploadHalalCertificateDocumentCommand(
    Guid CertificateId,
    IFormFile? File) : IRequest<HalalCertificateDetailResponse>;

public class UploadHalalCertificateDocumentValidator
    : AbstractValidator<UploadHalalCertificateDocumentCommand>
{
    public UploadHalalCertificateDocumentValidator()
    {
        RuleFor(x => x.CertificateId).NotEmpty();

        RuleFor(x => x.File)
            .NotNull().WithMessage("Document is required.");
    }
}

public class UploadHalalCertificateDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadHalalCertificateDocumentHandler> logger)
    : IRequestHandler<UploadHalalCertificateDocumentCommand, HalalCertificateDetailResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<HalalCertificateDetailResponse> Handle(
        UploadHalalCertificateDocumentCommand request,
        CancellationToken ct)
    {
        var certificate = await db.HalalCertificates
            .FirstOrDefaultAsync(
                row => row.Id == request.CertificateId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Halal certificate not found.");

        if (request.File is not { } file)
            throw new BusinessRuleException("Document is required.");

        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";
        var document = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var previous = certificate.Document;
        certificate.Document = document;

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
                    "Could not remove orphaned halal certificate document {StorageKey}",
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
                    "Could not remove replaced halal certificate document {StorageKey}",
                    previous.StorageKey);
            }
        }

        return await HalalCertificateDetailLoader.LoadAsync(db, certificate, ct);
    }
}
