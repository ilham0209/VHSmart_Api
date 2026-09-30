using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Features.Admin.WebLinks;

// The view/edit form of one Web Link (spec 5.4): every form field plus the icon file name
// (the bytes come from GET {id}/icon). The tenant filter answers 404 for another company's
// row. Shared by Get / Create / Update like CertificationBodyResponse is.
public record GetWebLinkByIdQuery(Guid Id) : IRequest<WebLinkResponse>;

public record WebLinkResponse(
    Guid Id,
    string Name,
    string Webpage,
    string? Description,
    string IconFileName,
    DateTime? ModifiedDate)
{
    internal static WebLinkResponse From(WebLinkEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Webpage,
            entity.Description,
            entity.Icon.FileName,
            entity.SysDateModified);
}

public class GetWebLinkByIdHandler(VHSmartDbContext db)
    : IRequestHandler<GetWebLinkByIdQuery, WebLinkResponse>
{
    public async Task<WebLinkResponse> Handle(
        GetWebLinkByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.WebLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.Id, ct);

        if (entity is null)
            throw new NotFoundException("Web link not found.");

        return WebLinkResponse.From(entity);
    }
}
