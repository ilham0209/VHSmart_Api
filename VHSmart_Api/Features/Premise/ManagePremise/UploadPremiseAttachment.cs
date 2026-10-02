using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// "Supporting Document" dialog of the Premise Attachment tab (spec 7.7): Type of Document
// (fixed - one of the D-15 list), Expiry Date, Reference No., Upload Document (PDF only,
// max 10 MB - D-22, validated server-side). One multipart form (spec 7.7): the bytes are
// stored before the row (CodingRules 10). Database.md 7 allows one current row per
// (PremiseId, DocumentType), so re-uploading a type REPLACES its row in place - the old
// bytes are dropped only after the save succeeded. Expiry Date and Reference No. travel
// with the file because the dialog always sends them together (legacy lets them be edited
// alone [CODE]; re-upload covers that until a dedicated edit endpoint exists).
public record UploadPremiseAttachmentCommand(
    Guid PremiseId,
    string DocumentType,
    DateTime? ExpiryDate,
    string? ReferenceNo,
    IFormFile? File) : IRequest<IReadOnlyList<PremiseAttachmentResponse>>;

public class UploadPremiseAttachmentValidator : AbstractValidator<UploadPremiseAttachmentCommand>
{
    public UploadPremiseAttachmentValidator()
    {
        RuleFor(x => x.PremiseId).NotEmpty();
        RuleFor(x => x.DocumentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Document type is required.")
            .MaximumLength(100).WithMessage("Document type must be 100 characters or fewer.")
            .Must(documentType => PremiseDocumentStatusCalculator.RequiredDocumentTypes
                .Contains(documentType, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Document type is invalid.");
        RuleFor(x => x.ReferenceNo)
            .MaximumLength(100).WithMessage("Reference number must be 100 characters or fewer.");
        RuleFor(x => x.File)
            .NotNull().WithMessage("Document is required.");
    }
}

public class UploadPremiseAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadPremiseAttachmentHandler> logger)
    : IRequestHandler<UploadPremiseAttachmentCommand, IReadOnlyList<PremiseAttachmentResponse>>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<IReadOnlyList<PremiseAttachmentResponse>> Handle(
        UploadPremiseAttachmentCommand request,
        CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var premiseExists = await db.Premises.AsNoTracking()
            .AnyAsync(row => row.Id == request.PremiseId && row.CompanyId == companyId, ct);
        if (!premiseExists)
            throw new NotFoundException("Premise not found.");

        if (request.File is not { } file)
            throw new BusinessRuleException("Document is required.");

        // D-22: premise attachments are PDF only, at most 10 MB - rejected before anything
        // is stored (BusinessRuleException -> 422).
        FileValidation.Validate(file.FileName, file.Length, FileValidation.PdfOnlyExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/pdf";
        var document = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        // The dialog's fixed list is the stored spelling, whatever casing the client sent.
        var documentType = PremiseDocumentStatusCalculator.RequiredDocumentTypes
            .First(candidate => string.Equals(candidate, request.DocumentType, StringComparison.OrdinalIgnoreCase));

        var entity = await db.PremiseAttachments.FirstOrDefaultAsync(
            row => row.PremiseId == request.PremiseId && row.DocumentType == documentType, ct);

        StoredFile? previous = null;
        if (entity is null)
        {
            entity = new PremiseAttachmentEntity
            {
                CompanyId = companyId,
                PremiseId = request.PremiseId,
                DocumentType = documentType,
                Document = document
            };
            db.PremiseAttachments.Add(entity);
        }
        else
        {
            previous = entity.Document;
            entity.Document = document;
        }

        entity.ExpiryDate = request.ExpiryDate;
        entity.ReferenceNo = request.ReferenceNo;

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
                    "Could not remove orphaned premise attachment {StorageKey}",
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
                    "Could not remove replaced premise attachment {StorageKey}",
                    previous.StorageKey);
            }
        }

        return await PremiseAttachmentListLoader.LoadAsync(db, companyId, request.PremiseId, ct);
    }
}
