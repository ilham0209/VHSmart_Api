using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Dashboard;

// Spec 13 "Total Expiry Typhoid Injection" panel [CONFIRMED, threshold VERIFY -> D-03]: the
// count plus the rows behind the panel's "View All Expiring Typhoid Injection" link, so one
// request serves both the number and the list. A row qualifies when it has a typhoid
// injection whose expiry runs out within the D-03 window - the calculator decides (already
// expired and the no-expiry sentinel are NOT "expiring"; the spec's word is Expiring, so
// lapsed injections are deliberately not mixed in [flagged]). Columns beyond name and
// expiry were not captured - the staff list's contact pair is offered [flagged]. Soonest
// expiry first (ours). Explicit CompanyId match like the All Staff list (PR-01 stance).
public record GetExpiringTyphoidInjectionQuery : IRequest<ExpiringTyphoidInjectionResponse>;

public record ExpiringTyphoidInjectionResponse(
    int Total,
    IReadOnlyList<ExpiringTyphoidStaffResponse> Items);

public record ExpiringTyphoidStaffResponse(
    Guid Id,
    string Name,
    string Email,
    string? MobileNumber,
    DateOnly TyphoidExpiryDate);

public class GetExpiringTyphoidInjectionHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IConfiguration configuration)
    : IRequestHandler<GetExpiringTyphoidInjectionQuery, ExpiringTyphoidInjectionResponse>
{
    public async Task<ExpiringTyphoidInjectionResponse> Handle(
        GetExpiringTyphoidInjectionQuery request,
        CancellationToken ct)
    {
        var today = PremiseDocumentClock.Today();
        var expiringDays = DashboardThresholds.ExpiringDays(configuration);

        var staff = await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.HasTyphoidInjection
                && row.TyphoidExpiryDate != null)
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.Email,
                row.MobileNumber,
                row.TyphoidExpiryDate
            })
            .ToListAsync(ct);

        var items = staff
            .Where(row => HalalStatusCalculator.IsExpiring(
                DateOnly.FromDateTime(row.TyphoidExpiryDate.GetValueOrDefault()),
                today,
                expiringDays))
            .OrderBy(row => row.TyphoidExpiryDate)
            .Select(row => new ExpiringTyphoidStaffResponse(
                row.Id,
                row.Name,
                row.Email,
                row.MobileNumber,
                DateOnly.FromDateTime(row.TyphoidExpiryDate.GetValueOrDefault())))
            .ToList();

        return new ExpiringTyphoidInjectionResponse(items.Count, items);
    }
}
