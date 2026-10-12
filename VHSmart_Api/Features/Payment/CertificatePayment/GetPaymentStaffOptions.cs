using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.CertificatePayment;

// Staff options behind the Submitted By* / Received By* pickers of the payment form
// (spec 15.1 "All Staff"): the caller's own company staff, Id + Name, searchable and
// ordered by name. The existing Staff > All Staff list is gated on the Staff permission,
// which a payment user does not necessarily hold, so the payment screen carries its own
// lookup (flagged). Behind Payment.Certificate View.
public record GetPaymentStaffOptionsQuery(string? SearchTerm)
    : IRequest<IReadOnlyList<PaymentStaffOptionResponse>>;

public record PaymentStaffOptionResponse(Guid Id, string Name);

public class GetPaymentStaffOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPaymentStaffOptionsQuery, IReadOnlyList<PaymentStaffOptionResponse>>
{
    public async Task<IReadOnlyList<PaymentStaffOptionResponse>> Handle(
        GetPaymentStaffOptionsQuery request,
        CancellationToken ct)
    {
        var term = request.SearchTerm?.Trim();

        return await db.Staffs
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (term == null || row.Name.Contains(term)))
            .OrderBy(row => row.Name)
            .Select(row => new PaymentStaffOptionResponse(row.Id, row.Name))
            .ToListAsync(ct);
    }
}
