using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.GroupAuditor;

// Group detail (spec 14.7 view action / edit modal preselect): header, the ChecklistId of
// the radio, the LeaderUserId and the live member ids. Unknown or foreign row -> 404.
public record GetGroupAuditorByIdQuery(Guid Id) : IRequest<GroupAuditorResponse>;

public class GetGroupAuditorByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetGroupAuditorByIdQuery, GroupAuditorResponse>
{
    public async Task<GroupAuditorResponse> Handle(
        GetGroupAuditorByIdQuery request,
        CancellationToken ct)
    {
        var entity = await db.GroupAuditors.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Group auditor not found.");

        var memberIds = await db.GroupAuditorMembers.AsNoTracking()
            .Where(row => row.GroupAuditorId == entity.Id)
            .Select(row => row.UserId)
            .ToListAsync(ct);

        return CreateGroupAuditorHandler.ToResponse(entity, memberIds);
    }
}
