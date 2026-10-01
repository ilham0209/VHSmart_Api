using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Notifications;

// The count beside the bell (spec 6.2): my unread rows only. The triggers that create rows
// belong to the tasks that own the action (D-25), so this reads whatever has been published.
public record GetUnreadCountQuery : IRequest<GetUnreadCountResponse>;

public record GetUnreadCountResponse(int Count);

public class GetUnreadCountHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<GetUnreadCountQuery, GetUnreadCountResponse>
{
    public async Task<GetUnreadCountResponse> Handle(
        GetUnreadCountQuery request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(caller.UserId, out var recipientId))
            throw new NotFoundException("User not found.");

        var count = await db.Notifications.AsNoTracking()
            .CountAsync(
                notification => notification.UserId == recipientId && !notification.IsRead,
                ct);

        return new GetUnreadCountResponse(count);
    }
}
