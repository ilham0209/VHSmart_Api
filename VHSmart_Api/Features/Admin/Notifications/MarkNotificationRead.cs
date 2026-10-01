using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Notifications;

// Marking read is per user: another user's id answers 404, never 403 and never succeeds -
// identity comes from the JWT, not the URL (CodingRules 8.1, CodingRules 9).
public record MarkNotificationReadCommand(Guid Id) : IRequest;

public class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class MarkNotificationReadHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(caller.UserId, out var recipientId))
            throw new NotFoundException("Notification not found.");

        var notification = await db.Notifications
            .SingleOrDefaultAsync(
                row => row.Id == request.Id && row.UserId == recipientId, ct)
            ?? throw new NotFoundException("Notification not found.");

        // Already read: a repeated bell click stays a successful no-op (idempotent PUT).
        if (notification.IsRead)
            return;

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
    }
}
