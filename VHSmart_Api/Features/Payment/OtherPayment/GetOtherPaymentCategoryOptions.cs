using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// "Payment Category*" dropdown of the Other payment form (spec 15.2: from General Data >
// PAYMENT > Payment Category): the caller's own company's General Data rows of that one
// Group + Category pair, Id + Name, ordered by name. The General Data screen is gated on its
// own permission, so the payment screen carries its own lookup (the same reason the
// Certificate screen carries a staff lookup - flagged). The category literal is the spec 5.1
// / GeneralDataCatalog value. Behind Payment.Other View.
public record GetOtherPaymentCategoryOptionsQuery
    : IRequest<IReadOnlyList<OtherPaymentCategoryOptionResponse>>;

public record OtherPaymentCategoryOptionResponse(Guid Id, string Name);

public class GetOtherPaymentCategoryOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetOtherPaymentCategoryOptionsQuery, IReadOnlyList<OtherPaymentCategoryOptionResponse>>
{
    // The Category literal of spec 5.1 / GeneralDataCatalog (PAYMENT group).
    private const string PaymentCategoryName = "Payment Category";

    public async Task<IReadOnlyList<OtherPaymentCategoryOptionResponse>> Handle(
        GetOtherPaymentCategoryOptionsQuery request,
        CancellationToken ct)
    {
        return await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.PAYMENT
                && row.Category == PaymentCategoryName)
            .OrderBy(row => row.Name)
            .Select(row => new OtherPaymentCategoryOptionResponse(row.Id, row.Name))
            .ToListAsync(ct);
    }
}
