using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// "Payee" link-lookup of the Other payment form (spec 15.2: link button → Admin > Reference
// Data > Service Provider): the caller's own company's Service Providers with the fields the
// form and the list show from them - Id, Name, Bank Name, Bank Account No. The Service
// Provider screen is gated on its own permission, which a payment user does not necessarily
// hold, so the payment screen carries its own lookup (the same reason the Certificate screen
// carries a staff lookup - flagged). Searchable, ordered by name. Behind Payment.Other View.
public record GetOtherPaymentPayeeOptionsQuery(string? SearchTerm)
    : IRequest<IReadOnlyList<OtherPaymentPayeeOptionResponse>>;

public record OtherPaymentPayeeOptionResponse(
    Guid Id,
    string Name,
    string? BankName,
    string? BankAccountNo);

public class GetOtherPaymentPayeeOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetOtherPaymentPayeeOptionsQuery, IReadOnlyList<OtherPaymentPayeeOptionResponse>>
{
    public async Task<IReadOnlyList<OtherPaymentPayeeOptionResponse>> Handle(
        GetOtherPaymentPayeeOptionsQuery request,
        CancellationToken ct)
    {
        var term = request.SearchTerm?.Trim();

        return await db.ServiceProviders
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (term == null || row.Name.Contains(term)))
            .OrderBy(row => row.Name)
            .Select(row => new OtherPaymentPayeeOptionResponse(
                row.Id,
                row.Name,
                row.BankName,
                row.BankAccountNo))
            .ToListAsync(ct);
    }
}
