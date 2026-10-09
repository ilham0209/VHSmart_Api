using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.HalalApplication.ManageBatch;

// The dropdown sources of the Manage Batch add/edit modals (spec 12.2): Scheme comes from
// the seeded global AdmSchemes (12.1 - the add modal offers every scheme incl. Food Premise),
// Brand Owner / Brand from the caller's own COMPANY General Data rows ("Brand Owner is
// required in every case" and flow 6 both pick one), and Manufacturer from the caller's own
// RawManufacturerSuppliers rows carrying a manufacturer half - the same set the batch
// validator accepts and the Manage Product form offers. One endpoint behind the
// HalalApplication.ManageBatch View action so the screen works without the Admin keys
// (the GetProductOptions / GetPremiseOptions reasoning). The add modal's "For Company"
// dropdown is NOT served: every screen in this codebase takes the company from the JWT only
// (CodingRules 8.1), so the client shows the caller's own company - flagged in the report.
public record GetBatchOptionsQuery : IRequest<BatchOptionsResponse>;

public record BatchOptionsResponse(
    IReadOnlyList<BatchOption> Schemes,
    IReadOnlyList<BatchOption> Brands,
    IReadOnlyList<BatchOption> Manufacturers);

public record BatchOption(Guid Id, string Name);

public class GetBatchOptionsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetBatchOptionsQuery, BatchOptionsResponse>
{
    public async Task<BatchOptionsResponse> Handle(
        GetBatchOptionsQuery request,
        CancellationToken ct)
    {
        var schemes = await db.Schemes
            .AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .Select(row => new BatchOption(row.Id, row.Name))
            .ToListAsync(ct);

        var brands = await db.GeneralData
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.Group == GeneralDataGroup.COMPANY
                && row.Category == "Brand")
            .OrderBy(row => row.Name)
            .Select(row => new BatchOption(row.Id, row.Name))
            .ToListAsync(ct);

        // A row with no manufacturer half is not a manufacturer (spec 9.1 "Manufacturer*").
        var manufacturers = await db.ManufacturerSuppliers
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId && row.ManufacturerName != null)
            .OrderBy(row => row.ManufacturerName)
            .Select(row => new BatchOption(row.Id, row.ManufacturerName!))
            .ToListAsync(ct);

        return new BatchOptionsResponse(schemes, brands, manufacturers);
    }
}
