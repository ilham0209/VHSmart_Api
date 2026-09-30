using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// Add a Supporting Document (spec 5.5 form): For View*, Document Type*, Document Sequence*,
// Is Mandatory?*, Description and - for the SOP views - the template file, one multipart form
// so the row is written only after the bytes are stored (CodingRules 10). Required fields
// follow Database.md 3. Duplicate rule (D-16): the same (Company, ForView, Sequence) is
// refused while the row is live, with the manual's message verbatim (spec 5.5). CompanyId
// comes from the JWT only (CodingRules 8.1). Template type/size follow D-22 defaults.
public record CreateSupportingDocumentCommand(
    SupportingDocumentForView? ForView,
    string DocumentType,
    int? DocumentSequence,
    bool IsMandatory,
    string? Description,
    IFormFile? Template) : IRequest<SupportingDocumentResponse>;

public class CreateSupportingDocumentValidator : AbstractValidator<CreateSupportingDocumentCommand>
{
    public CreateSupportingDocumentValidator()
    {
        // ForView / DocumentSequence are nullable so a missing form value fails validation
        // instead of silently defaulting to the first enum member / to 0.
        RuleFor(x => x.ForView)
            .NotNull().WithMessage("For View is required.")
            .IsInEnum().WithMessage("Unknown for view.");
        RuleFor(x => x.DocumentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Document Type is required.")
            .MaximumLength(200).WithMessage("Document Type must be 200 characters or fewer.");
        RuleFor(x => x.DocumentSequence)
            .NotNull().WithMessage("Document Sequence is required.");
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.");
        RuleFor(x => x.Template)
            .Must((command, template) => template is null
                || command.ForView is { } forView && SupportingDocumentCatalog.IsSopView(forView))
            .WithMessage("A template can only be uploaded for an SOP view.");
    }
}

public class CreateSupportingDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<CreateSupportingDocumentHandler> logger)
    : IRequestHandler<CreateSupportingDocumentCommand, SupportingDocumentResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<SupportingDocumentResponse> Handle(
        CreateSupportingDocumentCommand request,
        CancellationToken ct)
    {
        if (request.ForView is not { } forView)
            throw new BusinessRuleException("For View is required.");
        if (request.DocumentSequence is not { } sequence)
            throw new BusinessRuleException("Document Sequence is required.");

        var companyId = user.CompanyId;

        var duplicate = await db.SupportingDocuments.AnyAsync(row =>
            row.CompanyId == companyId
            && row.ForView == forView
            && row.DocumentSequence == sequence, ct);
        if (duplicate)
            throw new ConflictException(
                "Data document sequence exist. Please check the existing data.");

        StoredFile? template = null;
        if (request.Template is { } file)
        {
            FileValidation.Validate(file.FileName, file.Length);

            var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
                ? mapped
                : "application/octet-stream";
            template = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);
        }

        var entity = new SupportingDocumentEntity
        {
            CompanyId = companyId,
            ForView = forView,
            DocumentType = request.DocumentType,
            DocumentSequence = sequence,
            IsMandatory = request.IsMandatory,
            Description = request.Description,
            Template = template
        };
        db.SupportingDocuments.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row was rejected: drop the bytes we just wrote. A storage failure here must
            // not hide the original save error, so it is only logged.
            if (template is not null)
            {
                try
                {
                    await storage.DeleteAsync(template.StorageKey, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not remove orphaned template {StorageKey}", template.StorageKey);
                }
            }

            throw;
        }

        return SupportingDocumentResponse.From(entity);
    }
}
