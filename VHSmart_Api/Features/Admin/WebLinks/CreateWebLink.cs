using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.WebLinks;

// Add a Web Link (spec 5.4 form). Name*, Webpage* and the Icon* upload are one form, so the
// command carries the file and the row is written only after the bytes are stored - a failed
// save never leaves a half-created record (CodingRules 10). Required fields follow Database.md
// 3; the duplicate rule (spec 21.9) is the same Name within the company, handler-checked
// because Database.md 3 defines no unique index. CompanyId comes from the JWT only
// (CodingRules 8.1). Icon type/size follow D-22 through FileValidation's image list.
public record CreateWebLinkCommand(
    string Name,
    string Webpage,
    string? Description,
    IFormFile File) : IRequest<WebLinkResponse>;

public class CreateWebLinkValidator : AbstractValidator<CreateWebLinkCommand>
{
    public CreateWebLinkValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Webpage)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Webpage is required.")
            .MaximumLength(500).WithMessage("Webpage must be 500 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.File).NotNull().WithMessage("Icon is required.");
    }
}

public class CreateWebLinkHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<CreateWebLinkHandler> logger)
    : IRequestHandler<CreateWebLinkCommand, WebLinkResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<WebLinkResponse> Handle(CreateWebLinkCommand request, CancellationToken ct)
    {
        var companyId = user.CompanyId;

        var duplicate = await db.WebLinks.AnyAsync(row =>
            row.CompanyId == companyId && row.Name == request.Name, ct);
        if (duplicate)
            throw new ConflictException("A web link with this name already exists.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var icon = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var entity = new WebLinkEntity
        {
            CompanyId = companyId,
            Name = request.Name,
            Webpage = request.Webpage,
            Description = request.Description,
            Icon = icon
        };
        db.WebLinks.Add(entity);

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
                await storage.DeleteAsync(icon.StorageKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not remove orphaned icon {StorageKey}", icon.StorageKey);
            }

            throw;
        }

        return WebLinkResponse.From(entity);
    }
}
