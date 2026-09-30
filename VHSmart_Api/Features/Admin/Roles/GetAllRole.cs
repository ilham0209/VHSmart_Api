using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.Roles;

// Role list (spec 1.1 "Show N entries" table): Search box, sortable columns, paging.
public record GetAllRoleQuery : IRequest<DataGridResponse<GetAllRoleResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    Guid? CompanyId);

public class GetAllRoleHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllRoleQuery, DataGridResponse<GetAllRoleResponse>>
{
    public async Task<DataGridResponse<GetAllRoleResponse>> Handle(
        GetAllRoleQuery request,
        CancellationToken ct)
    {
        // Role Configuration is a platform-admin screen (D-07, spec 3.1); company users only
        // ever see the role they were given, never the role list.
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage roles.");

        return await db.Roles
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(RoleEntity.Name),
                nameof(RoleEntity.Description))
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .Select(role => new GetAllRoleResponse(
                role.Id, role.Name, role.Description, role.IsSystemRole, role.CompanyId))
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
