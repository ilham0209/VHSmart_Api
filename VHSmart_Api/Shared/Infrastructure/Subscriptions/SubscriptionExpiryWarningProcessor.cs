using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Shared.Infrastructure.Subscriptions;

// D-11: a warning 7 days before the subscription End Date, sent by the daily background job -
// never triggered from a GET (legacy defect, spec 23). One warning per period:
// ExpiryWarningSentAt is the idempotency stamp. There is no e-mail transport in this repo, so
// the warning is delivered through the in-app bell (D-25); the owner confirms the channel.
public class SubscriptionExpiryWarningProcessor(
    VHSmartDbContext db,
    INotificationService notifications)
{
    // Spec 21.8 names the template "Subscription Renewal Reminder".
    public const string Subject = "Subscription Renewal Reminder";

    // Returns how many subscription rows were warned about.
    public async Task<int> ProcessAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        // End Date - 7 days = today (the End Date itself is the day access is blocked, D-11).
        var dueEnd = today.Date.AddDays(7);

        // IgnoreQueryFilters: the job runs outside a request, so ICurrentUser carries no
        // company and the tenant filter would hide every row - this is a platform-wide scan.
        // The soft-delete filter is re-applied by hand.
        var due = await db.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                && x.ExpiryWarningSentAt == null
                && x.EndDate == dueEnd)
            .ToListAsync(cancellationToken);

        foreach (var group in due.GroupBy(x => x.CompanyId))
        {
            // Active users belonging to the company (memberships are not tenant-filtered, but
            // the soft-delete filter is off under IgnoreQueryFilters - re-applied by hand).
            var userIds = await db.Users
                .IgnoreQueryFilters()
                .Where(user => !user.IsDeleted
                    && user.IsActive
                    && db.UserCompanies.Any(membership => !membership.IsDeleted
                        && membership.UserId == user.Id
                        && membership.CompanyId == group.Key))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);

            // The expiry date the user sees is End Date - 1 day (spec 6.4, 13).
            var expiry = dueEnd.AddDays(-1).ToString("yyyy-MM-dd");
            await notifications.PublishManyAsync(
                userIds,
                Subject,
                $"Your subscription expires on {expiry}. Please renew to avoid interruption to your account.",
                linkUrl: null,
                companyId: group.Key,
                cancellationToken);
        }

        foreach (var row in due)
            row.ExpiryWarningSentAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
