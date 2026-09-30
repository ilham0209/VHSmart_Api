using MediatR;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Account.ProfilePicture;

// Account Setting > the current picture shown on the page and in the sidebar header
// (spec 6.4). The stored content type was derived from the extension at upload time, so the
// bytes can be streamed inline; a user without a picture answers 404 (no default image here -
// the frontend keeps its own placeholder).
public record GetProfilePictureQuery : IRequest<GetProfilePictureResponse>;

public record GetProfilePictureResponse(Stream Content, string ContentType);

public class GetProfilePictureHandler(
    VHSmartDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage)
    : IRequestHandler<GetProfilePictureQuery, GetProfilePictureResponse>
{
    public async Task<GetProfilePictureResponse> Handle(GetProfilePictureQuery request, CancellationToken ct)
    {
        var user = await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        var picture = user.ProfilePicture
            ?? throw new NotFoundException("No profile picture.");

        var content = await storage.OpenReadAsync(picture.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(picture.ContentType)
            ? "application/octet-stream"
            : picture.ContentType;

        return new GetProfilePictureResponse(content, contentType);
    }
}
