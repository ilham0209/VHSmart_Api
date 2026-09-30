using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.ServiceProviders;

// Service Provider list (spec 5.3 "Show N entries" table): Name, Description, Modified Date.
// The Action column (view / edit / delete / the unknown document icon) is client-side only.
// This is a [T] table: the global CompanyId filter scopes the rows to the caller's company
// (spec 3.3, Database.md 3), so no extra company check is needed here.
public record GetAllServiceProvidersQuery
    : IRequest<DataGridResponse<GetAllServiceProvidersResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllServiceProvidersResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime? ModifiedDate);

public class GetAllServiceProvidersHandler(VHSmartDbContext db)
    : IRequestHandler<GetAllServiceProvidersQuery, DataGridResponse<GetAllServiceProvidersResponse>>
{
    public async Task<DataGridResponse<GetAllServiceProvidersResponse>> Handle(
        GetAllServiceProvidersQuery request,
        CancellationToken ct) =>
        await db.ServiceProviders
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(ServiceProviderEntity.Name),
                nameof(ServiceProviderEntity.Description))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(row => new GetAllServiceProvidersResponse(
                row.Id,
                row.Name,
                row.Description,
                row.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
}
