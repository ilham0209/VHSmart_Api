using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.NonConformanceList;

// Detail of the "Manage Corrective Action Details" edit page (spec 14.11 [MANUAL]): the
// NC's own values plus the CURRENT state of both blocks - the latest auditee entry's
// Details/Attachment and the latest auditor entry's Comment/StatusSet - so the page can
// round-trip without loading the full history first (the history is a separate endpoint).
// Behind Audit.NonConformance View.
public record GetNonConformanceByIdQuery(Guid Id) : IRequest<NonConformanceDetailResponse>;

public record NonConformanceDetailResponse(
    Guid Id,
    string Source,
    string? AuditReferenceNo,
    string? ItemName,
    string? DetailName,
    string? Reference,
    string? Clause,
    string? Findings,
    string? NonConformanceDetails,
    Guid? CategoryId,
    string? Category,
    string? Remarks,
    string? Tags,
    string? Status,
    bool IsFinal,
    DateTime? ClosedAt,
    Guid? ClosedBy,
    string? CorrectiveActionDetails,
    string? CorrectiveActionAttachmentName,
    long? CorrectiveActionAttachmentSizeBytes,
    string? AuditorComment,
    string? AuditorStatusSet,
    int LatestVersion);

public class GetNonConformanceByIdValidator : AbstractValidator<GetNonConformanceByIdQuery>
{
    public GetNonConformanceByIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
    }
}

public class GetNonConformanceByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetNonConformanceByIdQuery, NonConformanceDetailResponse>
{
    public async Task<NonConformanceDetailResponse> Handle(
        GetNonConformanceByIdQuery request,
        CancellationToken ct)
    {
        var nc = await NonConformanceQueries.Own(db, user)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Non-conformance not found.");

        var categoryName = nc.CategoryId is null
            ? null
            : await db.GeneralData.AsNoTracking()
                  .Where(row => row.Id == nc.CategoryId.Value)
                  .Select(row => row.Name)
                  .FirstOrDefaultAsync(ct);

        var entries = await db.CorrectiveActionEntries.AsNoTracking()
            .Where(row => row.NonConformanceId == nc.Id)
            .OrderByDescending(row => row.Version)
            .ToListAsync(ct);

        var latestAuditee = entries.FirstOrDefault(row => row.FilledBy == CorrectiveActionFilledBy.Auditee);
        var latestAuditor = entries.FirstOrDefault(row => row.FilledBy == CorrectiveActionFilledBy.Auditor);

        return new NonConformanceDetailResponse(
            nc.Id,
            nc.Source.ToString(),
            nc.AuditReferenceNo,
            nc.ItemName,
            nc.DetailName,
            nc.Reference,
            nc.Clause,
            nc.Findings,
            nc.NonConformanceDetails,
            nc.CategoryId,
            categoryName,
            nc.Remarks,
            nc.Tags,
            nc.Status,
            nc.IsFinal,
            nc.ClosedAt,
            nc.ClosedBy,
            latestAuditee?.Details,
            latestAuditee?.Attachment?.FileName,
            latestAuditee?.Attachment?.SizeBytes,
            latestAuditor?.Comment,
            latestAuditor?.StatusSet,
            entries.Count == 0 ? 0 : entries[0].Version);
    }
}

// Shared tenant scope of the Non Conformance List features.
internal static class NonConformanceQueries
{
    public static IQueryable<NonConformanceEntity> Own(VHSmartDbContext db, ICurrentUser user) =>
        db.NonConformances.Where(row => row.CompanyId == user.CompanyId);
}
