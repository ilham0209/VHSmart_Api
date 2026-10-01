using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// The dropdown sources of the add/edit form (spec 3.2 form was never screenshotted, so the
// fields come from Database.md 5: Role and List of Company). Roles come from here rather
// than GET api/admin/roles because A-01 deliberately keeps the role list platform-admin
// only, while D-07 lets a company admin create users for their own company. Scope
// (D-07, spec 3.3): platform admin = every live company and role; company admin = their own
// companies and the system roles (CompanyId null, the three seeds of D-19).
public record GetUserOptionsQuery : IRequest<UserOptionsResponse>;

public record UserOptionsResponse(
    IReadOnlyList<UserOption> Roles,
    IReadOnlyList<UserOption> Companies);

public record UserOption(Guid Id, string Name);

public class GetUserOptionsHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<GetUserOptionsQuery, UserOptionsResponse>
{
    public async Task<UserOptionsResponse> Handle(
        GetUserOptionsQuery request,
        CancellationToken ct)
    {
        var roles = caller.IsPlatformAdmin
            ? await db.Roles.AsNoTracking()
                .OrderBy(role => role.Name)
                .Select(role => new UserOption(role.Id, role.Name))
                .ToListAsync(ct)
            : await db.Roles.AsNoTracking()
                .Where(role => role.CompanyId == null)
                .OrderBy(role => role.Name)
                .Select(role => new UserOption(role.Id, role.Name))
                .ToListAsync(ct);

        var companies = caller.IsPlatformAdmin
            ? await db.Companies.AsNoTracking()
                .OrderBy(company => company.Name)
                .Select(company => new UserOption(company.Id, company.Name))
                .ToListAsync(ct)
            : await db.Companies.AsNoTracking()
                .Where(company => company.Id == caller.CompanyId)
                .OrderBy(company => company.Name)
                .Select(company => new UserOption(company.Id, company.Name))
                .ToListAsync(ct);

        return new UserOptionsResponse(roles, companies);
    }
}
