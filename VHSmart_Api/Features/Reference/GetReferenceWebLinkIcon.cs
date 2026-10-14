using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Reference;

// The icon of one Reference card (spec 17.1: the cards show icon + title): streams the
// stored bytes inline for the Reference page. Its own endpoint, gated by Reference.View on
// the controller - the admin twin (R-05) sits behind Admin.WebLinks View and cannot serve a
// Reference-only user (CodingRules 10: a download needs the same permission as viewing the
// record). The tenant filter answers 404 for another company's row, same as the admin route.
public record GetReferenceWebLinkIconQuery(Guid Id)
    : IRequest<GetReferenceWebLinkIconResponse>;

public record GetReferenceWebLinkIconResponse(Stream Content, string ContentType);

public class GetReferenceWebLinkIconHandler(VHSmartDbContext db, IFileStorage storage)
    : IRequestHandler<GetReferenceWebLinkIconQuery, GetReferenceWebLinkIconResponse>
{
    public async Task<GetReferenceWebLinkIconResponse> Handle(
        GetReferenceWebLinkIconQuery request,
        CancellationToken ct)
    {
        var entity = await db.WebLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Web link not found.");

        var icon = entity.Icon;
        if (string.IsNullOrWhiteSpace(icon.StorageKey))
            throw new NotFoundException("No icon.");

        var content = await storage.OpenReadAsync(icon.StorageKey, ct);
        var contentType = string.IsNullOrWhiteSpace(icon.ContentType)
            ? "application/octet-stream"
            : icon.ContentType;

        return new GetReferenceWebLinkIconResponse(content, contentType);
    }
}
