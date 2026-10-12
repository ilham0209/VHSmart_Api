using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.NonConformanceList;

// "Transaction History > Corrective Action History" table (spec 14.11 [MANUAL]): Action
// (which side saved), Corrective Action Remarks (the auditee's Details or the auditor's
// Comment - one column on the form, mapped to whichever block the entry belongs to,
// flagged), Document Name (the auditee attachment), Version and Fill By (the saving
// user's name). Ordered by Version ascending - the append-only story reads forward.
// Behind Audit.NonConformance View.
public record GetCorrectiveActionHistoryQuery(Guid Id)
    : IRequest<IReadOnlyList<CorrectiveActionHistoryResponse>>;

public record CorrectiveActionHistoryResponse(
    Guid Id,
    string Action,
    string? Remarks,
    string? DocumentName,
    int Version,
    string? FillBy);

public class GetCorrectiveActionHistoryValidator : AbstractValidator<GetCorrectiveActionHistoryQuery>
{
    public GetCorrectiveActionHistoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
    }
}

public class GetCorrectiveActionHistoryHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetCorrectiveActionHistoryQuery, IReadOnlyList<CorrectiveActionHistoryResponse>>
{
    public async Task<IReadOnlyList<CorrectiveActionHistoryResponse>> Handle(
        GetCorrectiveActionHistoryQuery request,
        CancellationToken ct)
    {
        _ = await NonConformanceQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Non-conformance not found.");

        return await (
                from entry in db.CorrectiveActionEntries.AsNoTracking()
                where entry.NonConformanceId == request.Id
                orderby entry.Version
                select new CorrectiveActionHistoryResponse(
                    entry.Id,
                    entry.FilledBy.ToString(),
                    entry.Details ?? entry.Comment,
                    entry.Attachment != null ? entry.Attachment.FileName : null,
                    entry.Version,
                    (from filler in db.Users
                     where filler.Id == entry.FilledByUserId
                     select filler.Name).FirstOrDefault())
            ).ToListAsync(ct);
    }
}
