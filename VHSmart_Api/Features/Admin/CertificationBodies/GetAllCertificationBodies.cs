using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.CertificationBodies;

// CB list (spec 5.2 "Show N entries" table): Name, Country (display name, not a sort or
// search key), Notes, Modified Date. Platform-admin only (D-07): a company user never sees
// the CB list, they pick a CB from the dropdowns their own screens provide.
public record GetAllCertificationBodiesQuery
    : IRequest<DataGridResponse<GetAllCertificationBodiesResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllCertificationBodiesResponse(
    Guid Id,
    string Name,
    string? CountryName,
    string? Notes,
    DateTime? ModifiedDate);

public class GetAllCertificationBodiesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllCertificationBodiesQuery, DataGridResponse<GetAllCertificationBodiesResponse>>
{
    public async Task<DataGridResponse<GetAllCertificationBodiesResponse>> Handle(
        GetAllCertificationBodiesQuery request,
        CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage certification bodies.");

        return await db.CertificationBodies
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(CertificationBodyEntity.Name),
                nameof(CertificationBodyEntity.Notes))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(cb => new GetAllCertificationBodiesResponse(
                cb.Id,
                cb.Name,
                db.Countries
                    .Where(country => country.Id == cb.CountryId)
                    .Select(country => country.Name)
                    .FirstOrDefault(),
                cb.Notes,
                cb.SysDateModified))
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
