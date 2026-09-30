using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// Manage Certification Bodies > Logo (spec 5.2 "Logo (file)", legacy logo upload [CODE]):
// one image per CB stored as the File column group on AdmCertificationBodies (Database.md 3),
// same flow as the profile picture - bytes to IFileStorage first, row second, new file removed
// if the save fails (CodingRules 10). Type and size follow D-22 via FileValidation's
// image list; content type is derived from the extension so the browser is never handed a
// spoofed MIME. Platform-admin only (D-07).
public record UploadCertificationBodyLogoCommand(Guid Id, IFormFile File)
    : IRequest<UploadCertificationBodyLogoResponse>;

public record UploadCertificationBodyLogoResponse(string FileName, string ContentType, long SizeBytes);

public class UploadCertificationBodyLogoValidator
    : AbstractValidator<UploadCertificationBodyLogoCommand>
{
    public UploadCertificationBodyLogoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.File).NotNull();
    }
}

public class UploadCertificationBodyLogoHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadCertificationBodyLogoHandler> logger)
    : IRequestHandler<UploadCertificationBodyLogoCommand, UploadCertificationBodyLogoResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<UploadCertificationBodyLogoResponse> Handle(
        UploadCertificationBodyLogoCommand request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        var entity = await db.CertificationBodies
            .FirstOrDefaultAsync(cb => cb.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Certification body not found.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var stored = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var previous = entity.Logo;
        entity.Logo = stored;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        if (previous is not null)
        {
            try
            {
                await storage.DeleteAsync(previous.StorageKey, ct);
            }
            catch (Exception exception)
            {
                // The new logo is already saved; a stale byte file must not fail the request.
                logger.LogWarning(exception, "Could not delete the previous logo {StorageKey}", previous.StorageKey);
            }
        }

        return new UploadCertificationBodyLogoResponse(stored.FileName, stored.ContentType, stored.SizeBytes);
    }
}
