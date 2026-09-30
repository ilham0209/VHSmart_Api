using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Account.Profile;

// Account Setting > Profile (spec 6.4): read-only Name, Email and Role for the caller.
// Nothing on this screen is editable - Manage Users (C-02) owns the writable fields.
public record GetProfileQuery : IRequest<GetProfileResponse>;

public record GetProfileResponse(string Name, string Email, string RoleName);

public class GetProfileHandler(VHSmartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetProfileQuery, GetProfileResponse>
{
    public async Task<GetProfileResponse> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var user = await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        var roleName = await db.Roles.AsNoTracking()
            .Where(role => role.Id == user.RoleId)
            .Select(role => role.Name)
            .SingleOrDefaultAsync(ct)
            ?? string.Empty;

        return new GetProfileResponse(user.Name, user.Email, roleName);
    }
}
