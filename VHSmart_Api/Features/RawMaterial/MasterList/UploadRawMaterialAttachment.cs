using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.RawMaterial.MasterList;

// "Attachment Information" upload (spec 10.2): Type of Document picked from the company's own
// Supporting Document rows with For View = Raw Material (R-06), together with Expiry Date,
// Reference No., Authority and the file. D-22 file rules (pdf/docx/xlsx/jpg/jpeg/png, 10 MB -
// NOT the premise's PDF-only list) are checked server side before anything is stored
// (BusinessRuleException -> 422). Bytes are stored first, the row second (CodingRules 10). A
// second upload for the same type REPLACES the row in place so the section keeps one row per
// type; Database.md 8 states no uniqueness rule, so no index enforces it. Only the owning
// company may upload: a row shared with the caller is read-only and answers 404.
public record UploadRawMaterialAttachmentCommand(
    Guid RawMaterialId,
    Guid DocumentTypeId,
    DateTime? ExpiryDate,
    string? ReferenceNo,
    string? Authority,
    IFormFile? File) : IRequest<IReadOnlyList<RawMaterialAttachmentResponse>>;

public class UploadRawMaterialAttachmentValidator : AbstractValidator<UploadRawMaterialAttachmentCommand>
{
    public UploadRawMaterialAttachmentValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.RawMaterialId).NotEmpty();

        RuleFor(x => x.DocumentTypeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Document type is required.")
            .MustAsync((id, ct) => db.SupportingDocuments.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.ForView == SupportingDocumentForView.RawMaterial,
                ct))
            .WithMessage("Document type not found.");

        RuleFor(x => x.ReferenceNo)
            .MaximumLength(100).WithMessage("Reference number must be 100 characters or fewer.");
        RuleFor(x => x.Authority)
            .MaximumLength(200).WithMessage("Authority must be 200 characters or fewer.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Document is required.");
    }
}

public class UploadRawMaterialAttachmentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadRawMaterialAttachmentHandler> logger)
    : IRequestHandler<UploadRawMaterialAttachmentCommand, IReadOnlyList<RawMaterialAttachmentResponse>>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<IReadOnlyList<RawMaterialAttachmentResponse>> Handle(
        UploadRawMaterialAttachmentCommand request,
        CancellationToken ct)
    {
        var rawMaterial = await db.RawMaterials
            .FirstOrDefaultAsync(row => row.Id == request.RawMaterialId, ct);
        if (rawMaterial is null)
            throw new NotFoundException("Raw material not found.");

        // Sharing is read-only (CodingRules 7.3): only the owner company may add to the
        // section of a row another company can merely see.
        RawMaterialData.EnsureOwner(rawMaterial, user);

        if (request.File is not { } file)
            throw new BusinessRuleException("Document is required.");

        FileValidation.Validate(file.FileName, file.Length);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";
        var document = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = await db.RawMaterialAttachments.FirstOrDefaultAsync(
            row => row.RawMaterialId == rawMaterial.Id && row.DocumentTypeId == request.DocumentTypeId,
            ct);

        StoredFile? previous = null;
        if (entity is null)
        {
            entity = new RawMaterialAttachmentEntity
            {
                // The row's own company, not the caller's: a Switch Company = ALL token may
                // edit a row of another tenant and the attachment belongs with the material.
                CompanyId = rawMaterial.CompanyId,
                RawMaterialId = rawMaterial.Id,
                DocumentTypeId = request.DocumentTypeId,
                Document = document
            };
            db.RawMaterialAttachments.Add(entity);
        }
        else
        {
            previous = entity.Document;
            entity.Document = document;
        }

        entity.ExpiryDate = request.ExpiryDate;
        entity.ReferenceNo = request.ReferenceNo;
        entity.Authority = request.Authority;

        // The snapshot column of Database.md 8. Screens never read it: they recompute the
        // status from ExpiryDate (D-04), so the stored copy cannot go stale on a display.
        entity.DocumentStatus = HalalStatusCalculator
            .Status(
                HalalStatusCalculator.ExpiryForDisplay(
                    request.ExpiryDate is null
                        ? null
                        : DateOnly.FromDateTime(request.ExpiryDate.Value)),
                RawMaterialHalalInformation.Today())
            .ToString();

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
                    "Could not remove orphaned raw material attachment {StorageKey}",
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
                    "Could not remove replaced raw material attachment {StorageKey}",
                    previous.StorageKey);
            }
        }

        return await RawMaterialAttachmentListLoader.LoadAsync(db, rawMaterial.Id, ct);
    }
}
