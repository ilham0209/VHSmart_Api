using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// Staff options behind the Submitted By* / Received By* pickers of the Other payment form
// (spec 15.2 "link buttons → All Staff"): the caller's own company staff, Id + Name,
// searchable and ordered by name. Same reason the Certificate screen carries its own lookup
// (flagged): the Staff > All Staff list is gated on the Staff permission, which a payment
// user does not necessarily hold, and a vertical slice does not call another screen's
// handler. Behind Payment.Other View.
public record GetOtherPaymentStaffOptionsQuery(string? SearchTerm)
    : IRequest<IReadOnlyList<OtherPaymentStaffOptionResponse>>;

public record OtherPaymentStaffOptionResponse(Guid Id, string Name);

public class GetOtherPaymentStaffOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetOtherPaymentStaffOptionsQuery, IReadOnlyList<OtherPaymentStaffOptionResponse>>
{
    public async Task<IReadOnlyList<OtherPaymentStaffOptionResponse>> Handle(
        GetOtherPaymentStaffOptionsQuery request,
        CancellationToken ct)
    {
        var term = request.SearchTerm?.Trim();

        return await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (term == null || row.Name.Contains(term)))
            .OrderBy(row => row.Name)
            .Select(row => new OtherPaymentStaffOptionResponse(row.Id, row.Name))
            .ToListAsync(ct);
    }
}
