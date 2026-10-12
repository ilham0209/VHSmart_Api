using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// The "Audit Checklist" tab of spec 14.9: the Start Audit snapshot, ordered by the
// checklist numbering (CategorySequence then CriteriaSequence - the client renders the
// "No" column and the category/criteria headings from them). No search/paging is
// specified for this tab (only a refresh button), so the full list is returned in order
// (flagged). Editing goes through the item/final-point endpoints, not here.
public record GetAuditTaskItemsQuery(Guid Id)
    : IRequest<IReadOnlyList<GetAuditTaskItemsResponse>>;

public record GetAuditTaskItemsResponse(
    Guid Id,
    Guid? SourceAuditCriteriaId,
    string Category,
    int CategorySequence,
    string Criteria,
    int CriteriaSequence,
    string? SubCriteria,
    string? Reference,
    decimal PotentialPoint,
    decimal? ActualPoint,
    decimal? FinalPoint,
    bool? IsConforming,
    string? Remarks);

public class GetAuditTaskItemsValidator : AbstractValidator<GetAuditTaskItemsQuery>
{
    public GetAuditTaskItemsValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetAuditTaskItemsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditTaskItemsQuery, IReadOnlyList<GetAuditTaskItemsResponse>>
{
    public async Task<IReadOnlyList<GetAuditTaskItemsResponse>> Handle(
        GetAuditTaskItemsQuery request,
        CancellationToken ct)
    {
        _ = await AuditTaskQueries.VisibleTo(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Audit task not found.");

        return await (
                from item in db.AuditTaskItems.AsNoTracking()
                where item.AuditTaskId == request.Id
                orderby item.CategorySequence, item.CriteriaSequence, item.Id
                select new GetAuditTaskItemsResponse(
                    item.Id,
                    item.SourceAuditCriteriaId,
                    item.Category,
                    item.CategorySequence,
                    item.Criteria,
                    item.CriteriaSequence,
                    item.SubCriteria,
                    item.Reference,
                    item.PotentialPoint,
                    item.ActualPoint,
                    item.FinalPoint,
                    item.IsConforming,
                    item.Remarks))
            .ToListAsync(ct);
    }
}
