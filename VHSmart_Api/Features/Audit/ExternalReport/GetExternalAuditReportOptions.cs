using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Dropdown sources of the "Manage External Audit Report" header form (spec 14.10
// [VERIFY] sourcing, resolved by D-10): Type of Application <- General Data (AUDIT ->
// "External - Audit Category"), Type of Inspection <- General Data (AUDIT -> "External -
// Audit Reference") in that order (flagged: the category names read oddly against the
// field names, but D-10 lists them in that order), Application Scheme <- AdmSchemes,
// Received From <- the global AdmCertificationBodies list (D-07), Overall Status <- the
// D-10 fixed list Pass / Fail / Pending. "For Company*" is the caller's own company
// (CompanyId always comes from the JWT, flagged); the premise picker lists the own live
// premises for the "List of Premise" multi-select.
public record GetExternalAuditReportOptionsQuery : IRequest<ExternalReportOptionsResponse>;

public record ExternalReportOptionsResponse(
    string CompanyName,
    IReadOnlyList<ExternalReportIdNameOption> CertificationBodies,
    IReadOnlyList<string> TypeOfApplications,
    IReadOnlyList<string> TypeOfInspections,
    IReadOnlyList<ExternalReportIdNameOption> Schemes,
    IReadOnlyList<string> OverallStatuses,
    IReadOnlyList<ExternalReportPremiseOption> Premises);

public record ExternalReportIdNameOption(Guid Id, string Name);

public record ExternalReportPremiseOption(Guid Id, string Name, string? StoreCode);

// The D-10 reference data in one place so the create/update validators share it.
public static class ExternalReportReferenceData
{
    // D-10 order: Type of Application <- External - Audit Category,
    // Type of Inspection <- External - Audit Reference (flagged).
    public const string TypeOfApplicationCategory = "External - Audit Category";
    public const string TypeOfInspectionCategory = "External - Audit Reference";

    // Database.md 13: CategoryId -> AdmGeneralData (Non Compliance Category).
    public const string NonComplianceCategory = "Non Compliance Category";

    // D-10: fixed list until the owner defines the values.
    public static readonly IReadOnlyList<string> OverallStatuses = ["Pass", "Fail", "Pending"];
}

public class GetExternalAuditReportOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetExternalAuditReportOptionsQuery, ExternalReportOptionsResponse>
{
    public async Task<ExternalReportOptionsResponse> Handle(
        GetExternalAuditReportOptionsQuery request,
        CancellationToken ct)
    {
        var companyName = await db.Companies.AsNoTracking()
            .Where(row => row.Id == user.CompanyId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var certificationBodies = await db.CertificationBodies.AsNoTracking()
            .Where(row => !row.IsDeleted)
            .OrderBy(row => row.Name)
            .Select(row => new ExternalReportIdNameOption(row.Id, row.Name))
            .ToListAsync(ct);

        var typeOfApplications = await db.GeneralData.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == ExternalReportReferenceData.TypeOfApplicationCategory)
            .OrderBy(row => row.Name)
            .Select(row => row.Name)
            .ToListAsync(ct);

        var typeOfInspections = await db.GeneralData.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == ExternalReportReferenceData.TypeOfInspectionCategory)
            .OrderBy(row => row.Name)
            .Select(row => row.Name)
            .ToListAsync(ct);

        var schemes = await db.Schemes.AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name)
            .Select(row => new ExternalReportIdNameOption(row.Id, row.Name))
            .ToListAsync(ct);

        var premises = await db.Premises.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .OrderBy(row => row.Name)
            .Select(row => new ExternalReportPremiseOption(row.Id, row.Name, row.StoreCode))
            .ToListAsync(ct);

        return new ExternalReportOptionsResponse(
            companyName,
            certificationBodies,
            typeOfApplications,
            typeOfInspections,
            schemes,
            ExternalReportReferenceData.OverallStatuses,
            premises);
    }
}
