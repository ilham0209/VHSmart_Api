using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.MyApplication;

// Establishment tab of the application screen (spec 12.5): the premises the batch is linked
// to (live AppBatchPremises rows - Batch > Associate Premise owns them). The list carries the
// premise document status (D-15 calculator) so the tab shows COMPLETE DOCUMENTATION like
// Batch does. A batch-less application just has no establishments. Addresses are composed
// exactly like Premise > Manage Premise's list (Address1 + Address2 + Address3, trimmed).
public record GetApplicationEstablishmentsQuery(Guid Id)
    : IRequest<IReadOnlyList<ApplicationEstablishmentResponse>>;

public record ApplicationEstablishmentResponse(
    Guid PremiseId,
    string EstablishmentName,
    string Address,
    string Telephone,
    string? Fax,
    string DocumentStatus);

public class GetApplicationEstablishmentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetApplicationEstablishmentsQuery,
        IReadOnlyList<ApplicationEstablishmentResponse>>
{
    public async Task<IReadOnlyList<ApplicationEstablishmentResponse>> Handle(
        GetApplicationEstablishmentsQuery request,
        CancellationToken ct)
    {
        var application = await ApplicationData.FindAsync(db, user, request.Id, ct);
        var batchId = application.BatchId;
        if (batchId is null)
            return [];

        var premiseIds = await db.BatchPremises.AsNoTracking()
            .Where(row => row.BatchId == batchId)
            .Select(row => row.PremiseId)
            .ToListAsync(ct);
        if (premiseIds.Count == 0)
            return [];

        var premises = await db.Premises.AsNoTracking()
            .Where(row => premiseIds.Contains(row.Id))
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.Address1,
                row.Address2,
                row.Address3,
                row.Telephone,
                row.Fax
            })
            .ToListAsync(ct);

        var statuses = await BatchData.LoadPremiseStatusesAsync(db, premiseIds, ct);

        return premises
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row =>
            {
                var status = statuses.TryGetValue(row.Id, out var calculated)
                    ? calculated
                    : null;
                return new ApplicationEstablishmentResponse(
                    row.Id,
                    row.Name,
                    ((row.Address1 ?? string.Empty) + " "
                        + (row.Address2 ?? string.Empty) + " "
                        + (row.Address3 ?? string.Empty)).Trim(),
                    row.Telephone,
                    row.Fax,
                    status?.Text ?? PremiseDocumentStatusCalculator.NotCompleteText);
            })
            .ToList();
    }
}
