using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.SupportingDocuments;

// Edit a Supporting Document (spec 5.5). Same field rules as Create (kept in each feature file
// on purpose, CodingRules 4). The duplicate check excludes the row itself; soft-deleted rows
// are invisible to the query, so a freed sequence is reusable again (D-16). The template is
// part of the same form: a posted file replaces it, an omitted one keeps it, and moving the row
// out of the SOP views drops it - only SOP views may carry a template (spec 5.5).
public record UpdateSupportingDocumentCommand(
    Guid Id,
    SupportingDocumentForView? ForView,
    string DocumentType,
    int? DocumentSequence,
    bool IsMandatory,
    string? Description,
    IFormFile? Template) : IRequest<SupportingDocumentResponse>;

public class UpdateSupportingDocumentValidator : AbstractValidator<UpdateSupportingDocumentCommand>
{
    public UpdateSupportingDocumentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateSupportingDocumentHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UpdateSupportingDocumentHandler> logger)
    : IRequestHandler<UpdateSupportingDocumentCommand, SupportingDocumentResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<SupportingDocumentResponse> Handle(
        UpdateSupportingDocumentCommand request,
        CancellationToken ct)
    {
        var entity = await db.SupportingDocuments
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Supporting document not found.");

        if (request.ForView is not { } forView)
            throw new BusinessRuleException("For View is required.");
        if (request.DocumentSequence is not { } sequence)
            throw new BusinessRuleException("Document Sequence is required.");

        var duplicate = await db.SupportingDocuments.AnyAsync(row =>
            row.Id != entity.Id
            && row.CompanyId == user.CompanyId
            && row.ForView == forView
            && row.DocumentSequence == sequence, ct);
        if (duplicate)
            throw new ConflictException(
                "Data document sequence exist. Please check the existing data.");

        var replacement = request.Template;
        if (replacement is not null)
            FileValidation.Validate(replacement.FileName, replacement.Length);

        var previous = entity.Template;
        entity.ForView = forView;
        entity.DocumentType = request.DocumentType;
        entity.DocumentSequence = sequence;
        entity.IsMandatory = request.IsMandatory;
        entity.Description = request.Description;

        // Only the SOP views may carry a template (spec 5.5): moving the row to TRAINING etc.
        // drops it, and its bytes go with it after the save succeeds.
        if (!SupportingDocumentCatalog.IsSopView(forView))
            entity.Template = null;

        if (replacement is null)
        {
            await db.SaveChangesAsync(ct);

            if (entity.Template is null && previous is not null)
                await DeletePreviousAsync(previous.StorageKey, ct);

            return SupportingDocumentResponse.From(entity);
        }

        var contentType = ContentTypes.TryGetContentType(replacement.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";
        var template = await storage.SaveAsync(
            replacement.OpenReadStream(), replacement.FileName, contentType, ct);
        entity.Template = template;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row still carries the old template: drop only the bytes we just wrote, and
            // never let a storage error mask the save error (CodingRules 10).
            try
            {
                await storage.DeleteAsync(template.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not remove orphaned template {StorageKey}", template.StorageKey);
            }

            throw;
        }

        if (previous is not null)
            await DeletePreviousAsync(previous.StorageKey, ct);

        return SupportingDocumentResponse.From(entity);
    }

    private async Task DeletePreviousAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            await storage.DeleteAsync(storageKey, ct);
        }
        catch (Exception exception)
        {
            // The new template (or the cleared row) is already saved; a stale byte file must
            // not fail the request.
            logger.LogWarning(exception, "Could not delete the previous template {StorageKey}", storageKey);
        }
    }
}
