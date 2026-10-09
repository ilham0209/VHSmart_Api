using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditCriteria;

// The dropdown sources of the add/edit modal (spec 14.5: Category* dropdown, Criteria*
// searchable dropdown, Sub Criteria dropdown, Reference Category dropdown, Finding
// Selection dropdown) in one response - the GetBatchOptions precedent - behind the
// Audit.AuditCriteria View action so the screen works without the other screens' keys (the
// GetAuditPrefixOptions reasoning); every list is explicitly scoped to the caller's company
// so a ViewAll token must not read another tenant's data.
//   Categories: General Data Group AUDIT / "Internal - Audit Category" - the same probable
//     mapping the validator enforces (spec 14.1 [VERIFY], question 36, flagged).
//   Criteria / SubCriteria: the reusable master values the "+" creates.
//   Findings: the live findings the Finding Selection dropdown links to.
//   Reference Category: NO list - the source of its values is an open question (spec 14.5
//     [VERIFY], question 36); the modal field takes free text until the owner answers
//     (flagged).
public record GetAuditCriteriaOptionsQuery : IRequest<AuditCriteriaOptionsResponse>;

public record AuditCriteriaOptionsResponse(
    IReadOnlyList<AuditCriteriaCategoryOption> Categories,
    IReadOnlyList<AuditCriteriaMasterOption> Criteria,
    IReadOnlyList<AuditCriteriaMasterOption> SubCriteria,
    IReadOnlyList<AuditCriteriaFindingOption> Findings);

public record AuditCriteriaCategoryOption(Guid Id, string Name);

public record AuditCriteriaMasterOption(Guid Id, string Text);

public record AuditCriteriaFindingOption(Guid Id, string Name, string FindingCode);

public class GetAuditCriteriaOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditCriteriaOptionsQuery, AuditCriteriaOptionsResponse>
{
    public async Task<AuditCriteriaOptionsResponse> Handle(
        GetAuditCriteriaOptionsQuery request,
        CancellationToken ct)
    {
        var categories = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == CreateAuditCriteriaValidator.AuditCategory)
            .OrderBy(row => row.Name)
            .Select(row => new AuditCriteriaCategoryOption(row.Id, row.Name))
            .ToListAsync(ct);

        var criteria = await LoadMastersAsync(
            db, user.CompanyId, AuditCriteriaMasterKind.Criteria, ct);
        var subCriteria = await LoadMastersAsync(
            db, user.CompanyId, AuditCriteriaMasterKind.SubCriteria, ct);

        var findings = await db.Findings
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new AuditCriteriaFindingOption(
                row.Id, row.Name, row.FindingCode))
            .ToListAsync(ct);

        return new AuditCriteriaOptionsResponse(categories, criteria, subCriteria, findings);
    }

    private static async Task<IReadOnlyList<AuditCriteriaMasterOption>> LoadMastersAsync(
        VHSmartDbContext db,
        Guid companyId,
        AuditCriteriaMasterKind kind,
        CancellationToken ct) =>
        await db.AuditCriteriaMasters
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId && row.Kind == kind)
            .OrderBy(row => row.Text)
            .Select(row => new AuditCriteriaMasterOption(row.Id, row.Text))
            .ToListAsync(ct);
}
