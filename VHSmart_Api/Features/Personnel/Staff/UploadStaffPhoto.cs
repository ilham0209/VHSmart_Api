using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Staff;

// Photo upload of the Manage Staff modal (spec 7.4): one photo per staff row, stored as the
// optional File column group on ComStaff (Database.md 3). Type and size follow D-22 - only
// images are accepted (same rule as the A-03 profile picture); the content type is derived
// from the extension so the browser cannot be handed a spoofed value. Bytes go to
// IFileStorage first, the row is updated second and the new file is removed again if the save
// fails; the previous photo is deleted best-effort after a successful save (A-03 pattern).
public record UploadStaffPhotoCommand(Guid Id, IFormFile File)
    : IRequest<UploadStaffPhotoResponse>;

public class UploadStaffPhotoValidator : AbstractValidator<UploadStaffPhotoCommand>
{
    public UploadStaffPhotoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.File).NotNull().WithMessage("Photo is required.");
    }
}

public record UploadStaffPhotoResponse(string FileName, string ContentType, long SizeBytes);

public class UploadStaffPhotoHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage,
    ILogger<UploadStaffPhotoHandler> logger)
    : IRequestHandler<UploadStaffPhotoCommand, UploadStaffPhotoResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<UploadStaffPhotoResponse> Handle(
        UploadStaffPhotoCommand request,
        CancellationToken ct)
    {
        var staff = await db.Staffs
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff not found.");

        var file = request.File;
        FileValidation.Validate(file.FileName, file.Length, FileValidation.ImageExtensions);

        var contentType = ContentTypes.TryGetContentType(file.FileName, out var mapped)
            ? mapped
            : "application/octet-stream";

        var stored = await storage.SaveAsync(file.OpenReadStream(), file.FileName, contentType, ct);

        var previous = staff.Photo;
        staff.Photo = stored;

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
                // The new photo is already saved; a stale byte file must not fail the request.
                logger.LogWarning(exception, "Could not delete the previous staff photo {StorageKey}", previous.StorageKey);
            }
        }

        return new UploadStaffPhotoResponse(stored.FileName, stored.ContentType, stored.SizeBytes);
    }
}
