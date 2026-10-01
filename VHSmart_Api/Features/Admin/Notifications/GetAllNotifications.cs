using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.Notifications;

// Bell panel (spec 6.2, D-25): my rows only - the recipient is the JWT user, never a query
// parameter (a client-supplied user id is a legacy defect). Newest first unless the client
// sorts another column; the number beside the bell lives in GetUnreadCount.
public record GetAllNotificationsQuery : IRequest<DataGridResponse<GetAllNotificationsResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllNotificationsResponse(
    Guid Id,
    string Subject,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedDate);

public class GetAllNotificationsHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<GetAllNotificationsQuery, DataGridResponse<GetAllNotificationsResponse>>
{
    public async Task<DataGridResponse<GetAllNotificationsResponse>> Handle(
        GetAllNotificationsQuery request,
        CancellationToken ct)
    {
        // ICurrentUser carries the user id as a string (audit column); the row key is a Guid.
        if (!Guid.TryParse(caller.UserId, out var recipientId))
            throw new NotFoundException("User not found.");

        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort ? nameof(BaseClass.SysDateCreated) : request.Request.SortBy;
        var descending = defaultSort || request.Request.SortDescending;

        return await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == recipientId)
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(NotificationEntity.Subject),
                nameof(NotificationEntity.Message))
            .ApplySort(sortBy, descending)
            .Select(notification => new GetAllNotificationsResponse(
                notification.Id,
                notification.Subject,
                notification.Message,
                notification.LinkUrl,
                notification.IsRead,
                notification.SysDateCreated))
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
