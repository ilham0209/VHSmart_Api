using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.WebLinks;

// Web Link list (spec 5.4 "Show N entries" table): Name, Webpage, Description, Modified Date.
// The Action column (view / edit / delete) is client-side only; the icons themselves stream
// from GET {id}/icon. This is a [T] table, so the global CompanyId filter scopes the rows
// (spec 3.3, Database.md 3).
public record GetAllWebLinksQuery : IRequest<DataGridResponse<GetAllWebLinksResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllWebLinksResponse(
    Guid Id,
    string Name,
    string Webpage,
    string? Description,
    DateTime? ModifiedDate);

public class GetAllWebLinksHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllWebLinksQuery, DataGridResponse<GetAllWebLinksResponse>>
{
    public async Task<DataGridResponse<GetAllWebLinksResponse>> Handle(
        GetAllWebLinksQuery request,
        CancellationToken ct) =>
        await db.WebLinks
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(WebLinkEntity.Name),
                nameof(WebLinkEntity.Webpage),
                nameof(WebLinkEntity.Description))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(row => new GetAllWebLinksResponse(
                row.Id,
                row.Name,
                row.Webpage,
                row.Description,
                row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
}
