using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// The CB logo for the view form and the CB variations of 21.6 (spec 5.2): streams the stored
// bytes inline; a CB without a logo (or an unknown id) answers 404 - the frontend keeps its
// own placeholder. Platform-admin only (D-07), same gate as the list.
public record GetCertificationBodyLogoQuery(Guid Id) : IRequest<GetCertificationBodyLogoResponse>;

public record GetCertificationBodyLogoResponse(Stream Content, string ContentType);

public class GetCertificationBodyLogoHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IFileStorage storage)
    : IRequestHandler<GetCertificationBodyLogoQuery, GetCertificationBodyLogoResponse>
{
    public async Task<GetCertificationBodyLogoResponse> Handle(
        GetCertificationBodyLogoQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        var entity = await db.CertificationBodies
            .AsNoTracking()
            .FirstOrDefaultAsync(cb => cb.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Certification body not found.");

        var logo = entity.Logo
            ?? throw new NotFoundException("No logo.");

        var content = await storage.OpenReadAsync(logo.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(logo.ContentType)
            ? "application/octet-stream"
            : logo.ContentType;

        return new GetCertificationBodyLogoResponse(content, contentType);
    }
}
