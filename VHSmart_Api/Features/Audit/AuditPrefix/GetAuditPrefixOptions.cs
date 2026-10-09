using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPrefix;

// The Brand dropdown of the add/edit modal (spec 14.2 "Brand* dropdown from the company's
// Brands"): the caller's own COMPANY General Data "Brand" rows - the same set the validator
// accepts and GetBatchOptions / GetPremiseOptions serve. One endpoint behind the
// Audit.AuditPrefix View action so the screen works without the Admin.GeneralData key (the
// GetPremiseOptions reasoning); rows are explicitly scoped to the caller's company so a
// ViewAll token must not read another tenant's data.
public record GetAuditPrefixOptionsQuery : IRequest<AuditPrefixOptionsResponse>;

public record AuditPrefixOptionsResponse(IReadOnlyList<AuditPrefixOption> Brands);

public record AuditPrefixOption(Guid Id, string Name);

public class GetAuditPrefixOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditPrefixOptionsQuery, AuditPrefixOptionsResponse>
{
    public async Task<AuditPrefixOptionsResponse> Handle(
        GetAuditPrefixOptionsQuery request,
        CancellationToken ct)
    {
        var brands = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Brand")
            .OrderBy(row => row.Name)
            .Select(row => new AuditPrefixOption(row.Id, row.Name))
            .ToListAsync(ct);

        return new AuditPrefixOptionsResponse(brands);
    }
}
