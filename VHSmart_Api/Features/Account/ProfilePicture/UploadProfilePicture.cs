using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Account.ProfilePicture;

// Account Setting > Upload Profile Picture (spec 6.4): one picture per user, stored as the
// File column group on AdmUsers (Database.md 1). Bytes go to IFileStorage first, the row is
// updated second and the new file is removed again if the save fails, so a failed upload never
// leaves a half-created record (CodingRules 10). Type and size follow D-22 via FileValidation;
// the content type is derived from the extension here so the browser cannot be handed a
// spoofed value for an inline image.
public record UploadProfilePictureCommand(IFormFile File)
    : IRequest<UploadProfilePictureResponse>;

public class UploadProfilePictureValidator : AbstractValidator<UploadProfilePictureCommand>
{
    public UploadProfilePictureValidator()
    {
        RuleFor(x => x.File).NotNull();
    }
}

public record UploadProfilePictureResponse(string FileName, string ContentType, long SizeBytes);

public class UploadProfilePictureHandler(
    VHSmartDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    ILogger<UploadProfilePictureHandler> logger)
    : IRequestHandler<UploadProfilePictureCommand, UploadProfilePictureResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<UploadProfilePictureResponse> Handle(UploadProfilePictureCommand request, CancellationToken ct)
    {
        var user = await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var stored = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var previous = user.ProfilePicture;
        user.ProfilePicture = stored;

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
                // The new picture is already saved; a stale byte file must not fail the request.
                logger.LogWarning(exception, "Could not delete the previous profile picture {StorageKey}", previous.StorageKey);
            }
        }

        return new UploadProfilePictureResponse(stored.FileName, stored.ContentType, stored.SizeBytes);
    }
}
