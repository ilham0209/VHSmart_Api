using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.ExternalReport;

// Tab "List of Non Conformance" grid (spec 14.10): Action is client side; the columns are
// the spec grid's. Tags is stored, never derived (meaning unknown [VERIFY]). Corrective
// Action Details belongs to AudCorrectiveActionEntries (AU-10's table) and has no rows on
// an external report yet, so it is always empty here (flagged). No search/paging is
// specified for the tab (only Add), so the full list comes back in creation order
// (SysDateCreated, then Id; flagged). Behind Audit.ExternalReport View.
public record GetNonConformancesQuery(Guid Id)
    : IRequest<IReadOnlyList<NonConformanceResponse>>;

public record NonConformanceResponse(
    Guid Id,
    string? ItemName,
    string? DetailName,
    string? Reference,
    string? Clause,
    string? NonConformanceDetails,
    string? Remarks,
    string? Tags,
    string? Status,
    string? Category,
    string? CorrectiveActionDetails);

public class GetNonConformancesValidator : AbstractValidator<GetNonConformancesQuery>
{
    public GetNonConformancesValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetNonConformancesHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetNonConformancesQuery, IReadOnlyList<NonConformanceResponse>>
{
    public async Task<IReadOnlyList<NonConformanceResponse>> Handle(
        GetNonConformancesQuery request,
        CancellationToken ct)
    {
        _ = await ExternalReportQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("External audit report not found.");

        return await (
                from nonConformance in db.NonConformances.AsNoTracking()
                where nonConformance.ExternalAuditReportId == request.Id
                orderby nonConformance.SysDateCreated, nonConformance.Id
                select new NonConformanceResponse(
                    nonConformance.Id,
                    nonConformance.ItemName,
                    nonConformance.DetailName,
                    nonConformance.Reference,
                    nonConformance.Clause,
                    nonConformance.NonConformanceDetails,
                    nonConformance.Remarks,
                    nonConformance.Tags,
                    nonConformance.Status,
                    (from generalData in db.GeneralData
                     where generalData.Id == nonConformance.CategoryId
                     select generalData.Name).FirstOrDefault(),
                    // AudCorrectiveActionEntries is AU-10's table - no rows yet.
                    null))
            .ToListAsync(ct);
    }
}
