using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// Acknowledgement + Submit Report (spec 14.9 flow step 4, tab "Acknowledgement"
// [CONFIRMED]): one endpoint - the tab's only action is the Submit Report button, so the
// three required fields travel with the submit (a separate draft-save is not specified,
// flagged). Submit moves InProgress -> Completed, stamps End Audit Date + Time Out
// (server UTC, same [VERIFY] as Start) and locks the audit ([CONFIRMED]). Every checklist
// row must carry an Actual Point first - the flow says the auditor answers every row
// (message ours, flagged); D-06 then computes Audit Score = Sum(Actual)/Sum(Potential)x100
// (2 dp; unanswered would count as 0 but they cannot exist here). Final Review Score stays
// 0.00 until Final Points are entered. NC feed (AU-10): submit flips the task's Internal
// NCs to IsFinal so they surface on the Final Non Conformance List - the glossary
// ("after findings are finalized") points at submit because Completed locks the audit
// ([VERIFY] no explicit trigger in the spec; external NCs are not flipped - their
// finalize point is undefined, flagged). Generate Report is a later task.
public record SubmitAuditTaskCommand(
    Guid Id,
    string Summary,
    string AuditeeName,
    string AuditeeDesignation) : IRequest;

public class SubmitAuditTaskValidator : AbstractValidator<SubmitAuditTaskCommand>
{
    public SubmitAuditTaskValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.Summary)
            .NotEmpty()
            .WithMessage("Summary is required.")
            .MaximumLength(2000)
            .WithMessage("Summary cannot exceed 2000 characters.");

        RuleFor(x => x.AuditeeName)
            .NotEmpty()
            .WithMessage("Auditee Name is required.")
            .MaximumLength(200)
            .WithMessage("Auditee Name cannot exceed 200 characters.");

        RuleFor(x => x.AuditeeDesignation)
            .NotEmpty()
            .WithMessage("Auditee Designation is required.")
            .MaximumLength(200)
            .WithMessage("Auditee Designation cannot exceed 200 characters.");
    }
}

public class SubmitAuditTaskHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<SubmitAuditTaskCommand>
{
    public async Task Handle(SubmitAuditTaskCommand request, CancellationToken ct)
    {
        var task = await AuditTaskQueries.VisibleTo(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Audit task not found.");

        if (task.Status == AuditTaskStatus.NotStarted)
        {
            throw new ConflictException("The audit has not been started yet.");
        }

        if (task.Status == AuditTaskStatus.Completed)
        {
            throw new ConflictException("The audit has already been submitted.");
        }

        var items = await db.AuditTaskItems.AsNoTracking()
            .Where(row => row.AuditTaskId == task.Id)
            .ToListAsync(ct);

        if (items.Any(item => item.ActualPoint is null))
        {
            throw new BusinessRuleException(
                "All checklist rows must be answered before submitting.");
        }

        var now = DateTime.UtcNow;
        task.Summary = request.Summary;
        task.AuditeeName = request.AuditeeName;
        task.AuditeeDesignation = request.AuditeeDesignation;
        task.AuditScore = AuditTaskQueries.Score(items, item => item.ActualPoint);
        task.Status = AuditTaskStatus.Completed;
        task.EndAuditDate = now.Date;
        task.TimeOut = now.TimeOfDay;

        var itemIds = items.Select(item => item.Id).ToList();
        var internalNcs = await db.NonConformances
            .Where(row => row.Source == NonConformanceSource.Internal
                && row.AuditTaskItemId != null
                && itemIds.Contains(row.AuditTaskItemId.Value)
                && row.CompanyId == user.CompanyId)
            .ToListAsync(ct);
        foreach (var nc in internalNcs)
        {
            nc.IsFinal = true;
        }

        await db.SaveChangesAsync(ct);
    }
}
