using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Reference;

// Reference menu (spec 17.1 [CONFIRMED]/[MANUAL], 5.4 "Display"): the read-only grid of
// link cards behind Sidebar > Reference - icon + title, each card opening its Webpage in a
// new tab (client-side). Rows = the caller's own company's AdmWebLinks [T] with the explicit
// CompanyId match (spec 3.3, the house guard for tenant tables); the admin CRUD screen
// (R-05) stays behind Admin.WebLinks. The spec shows a fixed card grid with no search and no
// paging, so this answers a plain list sorted Name asc (the spec states no order - ours,
// flagged); the icons themselves stream from the companion endpoint below because the admin
// icon route is gated by Admin.WebLinks View, which a Reference-only user does not hold.
public record GetReferenceWebLinksQuery : IRequest<IReadOnlyList<ReferenceLinkResponse>>;

public record ReferenceLinkResponse(
    Guid Id,
    string Name,
    string Webpage,
    string? Description);

public class GetReferenceWebLinksHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetReferenceWebLinksQuery, IReadOnlyList<ReferenceLinkResponse>>
{
    public async Task<IReadOnlyList<ReferenceLinkResponse>> Handle(
        GetReferenceWebLinksQuery request,
        CancellationToken ct) =>
        await db.WebLinks
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new ReferenceLinkResponse(
                row.Id,
                row.Name,
                row.Webpage,
                row.Description))
            .ToListAsync(ct);
}
