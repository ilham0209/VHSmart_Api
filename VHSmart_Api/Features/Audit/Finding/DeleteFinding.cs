using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.Finding;

// Remove a finding (spec 14.4 trash action): soft delete the finding AND its live
// recommendation links - the links belong to the finding and would otherwise orphan
// (Database.md 1 conventions, the GetAllBatches unlink precedent). Unknown or foreign row
// -> 404 (CodingRules 9). Spec 14.4 / 5.6 [CODE] "Deleting a Finding sends an in-app
// notification": recipients are every ACTIVE user of the caller's company except the user
// performing the delete (spec names no recipient - the SubscriptionExpiryWarningProcessor
// "all active company users" precedent minus the actor; flagged).
public record DeleteFindingCommand(Guid Id) : IRequest;

public class DeleteFindingHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    INotificationService notifications)
    : IRequestHandler<DeleteFindingCommand>
{
    public const string NotificationSubject = "Finding deleted";

    public async Task Handle(DeleteFindingCommand request, CancellationToken ct)
    {
        var entity = await db.Findings
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct);

        if (entity is null)
            throw new NotFoundException("Finding not found.");

        var links = await db.FindingRecommendations
            .Where(link => link.FindingId == entity.Id)
            .ToListAsync(ct);

        // Children first, then the parent (the DeleteUser order): the FK is non-nullable
        // and the edge is Restrict, so severing the association before the parent is
        // marked deleted keeps EF from demanding a cascade it does not have.
        foreach (var link in links)
            db.FindingRecommendations.Remove(link);
        db.Findings.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Memberships are not tenant-filtered, but both global soft-delete filters apply.
        // ICurrentUser.UserId is the token's string form (the Guid.TryParse pattern of
        // CreateApplication); the actor never receives their own delete notification.
        var actorId = Guid.TryParse(user.UserId, out var parsed) ? parsed : (Guid?)null;
        var userIds = await db.Users
            .Where(candidate => candidate.IsActive
                && (actorId == null || candidate.Id != actorId)
                && db.UserCompanies.Any(membership => membership.UserId == candidate.Id
                    && membership.CompanyId == user.CompanyId))
            .Select(candidate => candidate.Id)
            .ToListAsync(ct);

        await notifications.PublishManyAsync(
            userIds,
            NotificationSubject,
            $"The finding \"{entity.FindingCode}\" was deleted.",
            linkUrl: null,
            companyId: user.CompanyId,
            ct);
    }
}
