using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Account.Subscription;

// Account Setting > Subscription tab, header fields (spec 6.4): the current period is the
// row with the latest End Date. Next Renewal Date = that End Date and Expiry Date = one day
// before it (the display rule of spec 6.4 and 13). End Date 9999-12-31 = "Ongoing": both
// dates come back blank and the flag tells the screen which word to show.
public record GetSubscriptionQuery : IRequest<GetSubscriptionResponse>;

public record GetSubscriptionResponse(
    string? PackageCode,
    string? PackageName,
    DateTime? ExpiryDate,
    DateTime? NextRenewalDate,
    bool IsOngoing);

public class GetSubscriptionHandler(VHSmartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSubscriptionQuery, GetSubscriptionResponse>
{
    public async Task<GetSubscriptionResponse> Handle(GetSubscriptionQuery request, CancellationToken ct)
    {
        // Same identity preamble as the rest of Account Setting: soft-deleted 404, inactive 403.
        await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        // The company comes from the JWT; the explicit match keeps a ViewAllCompanies caller
        // (whose tenant filter is wide open) on their own subscription rows.
        var latest = await db.CompanySubscriptions.AsNoTracking()
            .Where(x => x.CompanyId == currentUser.CompanyId)
            .OrderByDescending(x => x.EndDate)
            .ThenByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
            return new GetSubscriptionResponse(null, null, null, null, false);

        var package = await db.SubscriptionPackages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == latest.PackageId, ct);

        // Reuses the shared expiry rule (CodingRules 11): the sentinel date never expires.
        var ongoing = HalalStatusCalculator.HasNoExpiry(DateOnly.FromDateTime(latest.EndDate));

        return new GetSubscriptionResponse(
            package?.Code,
            package?.Name,
            ongoing ? null : latest.EndDate.AddDays(-1),
            ongoing ? null : latest.EndDate,
            ongoing);
    }
}
