using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Premise.ManagePremise;

// Halal Information tab of the premise modal (spec 7.7 [CONFIRMED]: #, VH SMART Reference
// No., CB Reference No., Scheme, Application Status, Halal Certificate No., Halal Certificate
// Status, Halal Expiry Date) - a numbered list of ALL applications for that premise (the same
// data Advanced Search shows, spec 8). The premise reaches its applications only through
// AppBatchPremises -> AppBatches -> AppHalalApplications (Database.md 12): a premise that was
// never associated to a batch answers an empty tab, which is also the fate of most Factory
// premises under JAKIM, where product batches carry products + manufacturer instead of
// premises (spec 12.2) - flagged for the owner rather than bridged with an invented join.
// Newest application first (the spec shows no order - ours, flagged in the report).
//
// Certificate columns: one application may hold SEVERAL certificates (HA-06) and the spec
// never says which one the cell shows, so the values appear only when the application has
// exactly one - with several the cell stays empty instead of inventing an earliest/latest
// rule (same stance as My Application's Halal Expiry Date; Q17). Status and expiry are the
// shared D-04 calculators over the certificate's expiry date.
public record GetPremiseHalalInformationQuery(Guid PremiseId)
    : IRequest<IReadOnlyList<PremiseHalalInfoRow>>;

public record PremiseHalalInfoRow(
    Guid ApplicationId,
    string VhSmartReferenceNo,
    string? CbReferenceNo,
    string Scheme,
    string ApplicationStatus,
    string? HalalCertificateNo,
    HalalStatus? HalalCertificateStatus,
    DateOnly? HalalExpiryDate);

public class GetPremiseHalalInformationHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetPremiseHalalInformationQuery, IReadOnlyList<PremiseHalalInfoRow>>
{
    public async Task<IReadOnlyList<PremiseHalalInfoRow>> Handle(
        GetPremiseHalalInformationQuery request,
        CancellationToken ct)
    {
        // The explicit CompanyId match keeps a foreign premise at 404 (never 403), the
        // stance of every other read in this controller.
        var premiseExists = await db.Premises
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId, ct);
        if (!premiseExists)
            throw new NotFoundException("Premise not found.");

        var rows = await PremiseHalalInfoLoader.LoadAsync(
            db, user, [request.PremiseId], ct);
        return rows.GetValueOrDefault(request.PremiseId) ?? [];
    }
}

// One loader for the tab (all rows of one premise) and for the Premise List (the newest row
// of each premise on the page): both are read-only joins over the same three tables, filled
// AFTER paging so they never take part in search or sort - the same stance as Document
// Status. Rows come back newest StatusDate first per premise, ties broken by Id so the order
// is deterministic.
internal static class PremiseHalalInfoLoader
{
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PremiseHalalInfoRow>>>
        LoadAsync(
            VHSmartDbContext db,
            ICurrentUser user,
            IReadOnlyCollection<Guid> premiseIds,
            CancellationToken ct)
    {
        if (premiseIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<PremiseHalalInfoRow>>();

        var applications = await (
                from premiseLink in db.BatchPremises.AsNoTracking()
                join application in db.Applications.AsNoTracking()
                    on premiseLink.BatchId equals application.BatchId
                where premiseIds.Contains(premiseLink.PremiseId)
                    && premiseLink.CompanyId == user.CompanyId
                    && application.CompanyId == user.CompanyId
                orderby application.StatusDate descending, application.Id descending
                select new ApplicationJoin(
                    premiseLink.PremiseId,
                    application.Id,
                    application.ReferenceNo,
                    application.Status,
                    application.SchemeId,
                    application.BatchId))
            .ToListAsync(ct);
        if (applications.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<PremiseHalalInfoRow>>();

        var applicationIds = applications.Select(row => row.Id).Distinct().ToList();
        var certificates = await db.HalalCertificates
            .AsNoTracking()
            .Where(row => applicationIds.Contains(row.ApplicationId)
                && row.CompanyId == user.CompanyId)
            .ToListAsync(ct);
        var certificatesByApplication = certificates
            .GroupBy(row => row.ApplicationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var schemeNames = await db.Schemes
            .AsNoTracking()
            .Where(row => applications.Select(application => application.SchemeId)
                .Distinct()
                .Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Name, ct);

        var batchIds = applications
            .Select(row => row.BatchId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var cbReferenceByBatch = await db.Batches
            .AsNoTracking()
            .Where(row => batchIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.CbReferenceNo, ct);

        var today = PremiseDocumentClock.Today();
        return applications
            .GroupBy(row => row.PremiseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PremiseHalalInfoRow>)group
                    .Select(application => Project(
                        application,
                        certificatesByApplication.GetValueOrDefault(application.Id),
                        schemeNames.GetValueOrDefault(application.SchemeId) ?? string.Empty,
                        application.BatchId is null
                            ? null
                            : cbReferenceByBatch.GetValueOrDefault(application.BatchId.Value),
                        today))
                    .ToList());
    }

    private sealed record ApplicationJoin(
        Guid PremiseId,
        Guid Id,
        string ReferenceNo,
        string Status,
        Guid SchemeId,
        Guid? BatchId);

    private static PremiseHalalInfoRow Project(
        ApplicationJoin application,
        List<HalalCertificateEntity>? certificates,
        string scheme,
        string? cbReferenceNo,
        DateOnly today)
    {
        // Exactly one certificate -> its number, status and expiry; none or several ->
        // empty cells (Q17, the stance of the My Application list).
        string? certificateNumber = null;
        HalalStatus? certificateStatus = null;
        DateOnly? expiryDate = null;
        if (certificates is [var certificate])
        {
            expiryDate = HalalStatusCalculator.ExpiryForDisplay(certificate.ExpiryDate);
            certificateNumber = certificate.CertificateNo;
            certificateStatus = HalalStatusCalculator.Status(expiryDate, today);
        }

        return new PremiseHalalInfoRow(
            application.Id,
            application.ReferenceNo,
            cbReferenceNo,
            scheme,
            application.Status,
            certificateNumber,
            certificateStatus,
            expiryDate);
    }
}
