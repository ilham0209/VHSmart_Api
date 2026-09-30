using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Features.Admin.WebLinks;

// The icon of one Web Link (spec 5.4 "Icon (file upload)"): streams the stored bytes inline
// for the view/edit form. The public Reference menu cards (spec 17.1, task DS-04) get their
// own endpoint then - this one is gated by the Admin.WebLinks View permission (CodingRules 10:
// a download needs the same permission as viewing the record). The tenant filter answers 404
// for another company's row.
public record GetWebLinkIconQuery(Guid Id) : IRequest<GetWebLinkIconResponse>;

public record GetWebLinkIconResponse(Stream Content, string ContentType);

public class GetWebLinkIconHandler(VHSmartDbContext db, IFileStorage storage)
    : IRequestHandler<GetWebLinkIconQuery, GetWebLinkIconResponse>
{
    public async Task<GetWebLinkIconResponse> Handle(
        GetWebLinkIconQuery request,
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

        return new GetWebLinkIconResponse(content, contentType);
    }
}
