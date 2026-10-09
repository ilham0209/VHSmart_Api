using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditChecklist;

// The Checklist Category dropdown of the add/edit modal (spec 14.6 "Checklist Category*
// (dropdown)"): General Data Group AUDIT / "Internal - Audit Category" - the same probable
// §14.1 mapping the validator enforces ("Manage Audit Checklist > Checklist Category <- an
// audit-category list", [VERIFY] - flagged, AU-04 carries the same flag). Sample values
// seen in the spec ("Internal Supplier", "Syariah") are ordinary rows of that list.
// Explicitly company scoped so a ViewAll token must not read another tenant's reference
// data (the GetAuditPrefixOptions reasoning); behind the Audit.AuditChecklist View action
// so the screen needs no other menu's permission.
//   The Criteria Selection table is a separate paged endpoint (GetAuditChecklistCriteria
//   Options) because spec 14.6 gives it search + paging; Reference Category has no bearing
//   on this screen.
public record GetAuditChecklistOptionsQuery : IRequest<AuditChecklistOptionsResponse>;

public record AuditChecklistOptionsResponse(
    IReadOnlyList<AuditChecklistCategoryOption> Categories);

public record AuditChecklistCategoryOption(Guid Id, string Name);

public class GetAuditChecklistOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditChecklistOptionsQuery, AuditChecklistOptionsResponse>
{
    public async Task<AuditChecklistOptionsResponse> Handle(
        GetAuditChecklistOptionsQuery request,
        CancellationToken ct)
    {
        var categories = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == CreateAuditChecklistValidator.ChecklistCategory)
            .OrderBy(row => row.Name)
            .Select(row => new AuditChecklistCategoryOption(row.Id, row.Name))
            .ToListAsync(ct);

        return new AuditChecklistOptionsResponse(categories);
    }
}
