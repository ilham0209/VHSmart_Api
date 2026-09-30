using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Roles;

// The matrix of one role for the assign screen: every screen key with its four flags, denied
// keys included, so the client can render the checkboxes as they are stored.
public record GetRolePermissionsQuery(Guid RoleId) : IRequest<RolePermissionsResponse>;

public class GetRolePermissionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetRolePermissionsQuery, RolePermissionsResponse>
{
    public async Task<RolePermissionsResponse> Handle(GetRolePermissionsQuery request, CancellationToken ct)
    {
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage roles.");

        var role = await db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.RoleId, ct);

        if (role is null)
            throw new NotFoundException("Role not found.");

        var rows = await db.RolePermissions
            .AsNoTracking()
            .Where(row => row.RoleId == role.Id)
            .ToListAsync(ct);

        return new RolePermissionsResponse(role.Id, role.Name, RolePermissionMatrix.Build(rows));
    }
}
