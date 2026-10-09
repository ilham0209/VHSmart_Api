using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Product.VerifyHalalProductUpdate;

// The "View By" dropdown source of the Verify Halal Product Update list (spec 9.4): the
// caller's own PRODUCT / "Product Category" General Data rows. One endpoint behind the
// Product.VerifyHalalProductUpdate View action so the screen works without the Admin keys
// (same reasoning as the product and raw material options endpoints - the Admin General Data
// CRUD is platform-admin only).
public record GetVerifyHalalCategoryOptionsQuery
    : IRequest<IReadOnlyList<VerifyHalalCategoryOption>>;

public record VerifyHalalCategoryOption(Guid Id, string Name);

public class GetVerifyHalalCategoryOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetVerifyHalalCategoryOptionsQuery, IReadOnlyList<VerifyHalalCategoryOption>>
{
    public async Task<IReadOnlyList<VerifyHalalCategoryOption>> Handle(
        GetVerifyHalalCategoryOptionsQuery request,
        CancellationToken ct) =>
        await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.PRODUCT
                && row.Category == ProductGeneralDataCategory.ProductCategory)
            .OrderBy(row => row.Name)
            .Select(row => new VerifyHalalCategoryOption(row.Id, row.Name))
            .ToListAsync(ct);
}
