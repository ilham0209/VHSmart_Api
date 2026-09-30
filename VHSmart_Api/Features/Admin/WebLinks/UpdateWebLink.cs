using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.WebLinks;

// Edit a Web Link (spec 5.4). Same field rules as Create (kept in each feature file on
// purpose, CodingRules 4); the icon is part of the same form, so a posted file replaces it and
// an omitted one keeps the current icon - the row is never left without one (Icon*). The
// duplicate-name check excludes the row itself; soft-deleted rows are invisible to the query,
// so a deleted link's name is free again. The tenant filter answers 404 for another company.
public record UpdateWebLinkCommand(
    Guid Id,
    string Name,
    string Webpage,
    string? Description,
    IFormFile? File) : IRequest<WebLinkResponse>;

public class UpdateWebLinkValidator : AbstractValidator<UpdateWebLinkCommand>
{
    public UpdateWebLinkValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Webpage)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Webpage is required.")
            .MaximumLength(500).WithMessage("Webpage must be 500 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class UpdateWebLinkHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UpdateWebLinkHandler> logger)
    : IRequestHandler<UpdateWebLinkCommand, WebLinkResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<WebLinkResponse> Handle(UpdateWebLinkCommand request, CancellationToken ct)
    {
        var entity = await db.WebLinks
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Web link not found.");

        var duplicate = await db.WebLinks.AnyAsync(row =>
            row.Id != entity.Id
            && row.CompanyId == user.CompanyId
            && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A web link with this name already exists.");

        var replacement = request.File;
        if (replacement is not null)
            FileValidation.Validate(replacement.FileName, replacement.Length, FileValidation.ImageExtensions);

        var previous = entity.Icon;
        entity.Name = request.Name;
        entity.Webpage = request.Webpage;
        entity.Description = request.Description;

        if (replacement is null)
        {
            await db.SaveChangesAsync(ct);
            return WebLinkResponse.From(entity);
        }

        var contentType = ContentTypes.TryGetContentType(replacement.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";
        var icon = await storage.SaveAsync(
            replacement.OpenReadStream(), replacement.FileName, contentType, ct);
        entity.Icon = icon;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // The row still carries the old icon: drop only the bytes we just wrote, and never
            // let a storage error mask the save error (CodingRules 10).
            try
            {
                await storage.DeleteAsync(icon.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not remove orphaned icon {StorageKey}", icon.StorageKey);
            }

            throw;
        }

        try
        {
            await storage.DeleteAsync(previous.StorageKey, ct);
        }
        catch (Exception exception)
        {
            // The new icon is already saved; a stale byte file must not fail the request.
            logger.LogWarning(exception, "Could not delete the previous icon {StorageKey}", previous.StorageKey);
        }

        return WebLinkResponse.From(entity);
    }
}
