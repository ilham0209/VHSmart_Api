using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Personnel.Staff;

// The photo shown in the Manage Staff modal (spec 7.4 "Photo upload (default avatar shown)").
// The stored content type was derived from the extension at upload time, so the bytes stream
// inline; a staff row without a photo answers 404 (no default image here - the frontend keeps
// its own placeholder, same as A-03). The explicit CompanyId match scopes the row.
public record GetStaffPhotoQuery(Guid Id) : IRequest<GetStaffPhotoResponse>;

public record GetStaffPhotoResponse(Stream Content, string ContentType);

public class GetStaffPhotoHandler(VHSmartDbContext db, ICurrentUser user, IFileStorage storage)
    : IRequestHandler<GetStaffPhotoQuery, GetStaffPhotoResponse>
{
    public async Task<GetStaffPhotoResponse> Handle(GetStaffPhotoQuery request, CancellationToken ct)
    {
        var staff = await db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Staff not found.");

        var picture = staff.Photo
            ?? throw new NotFoundException("No photo.");

        var content = await storage.OpenReadAsync(picture.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(picture.ContentType)
            ? "application/octet-stream"
            : picture.ContentType;

        return new GetStaffPhotoResponse(content, contentType);
    }
}
