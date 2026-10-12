using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// Final Review of one checklist row (D-06, cited by AU-08): Final Point is editable only
// AFTER the audit is Completed - the one write that survives the Submit lock (spec 14.9
// locks everything else [CONFIRMED]). D-06 says "Chief Auditor only", but the seeded role
// model has a single Auditor/Chief Auditor role (D-19), so chief-vs-auditor cannot be
// enforced - the gate is the Audit.AuditTask permission plus the Completed status
// (flagged). Each save recomputes Final Review Score = Sum(Final)/Sum(Potential)x100
// (2 dp; rows without a Final Point count as 0 until entered).
public record UpdateAuditTaskFinalPointCommand(
    Guid Id,
    Guid ItemId,
    decimal? FinalPoint) : IRequest;

public class UpdateAuditTaskFinalPointValidator
    : AbstractValidator<UpdateAuditTaskFinalPointCommand>
{
    public UpdateAuditTaskFinalPointValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.ItemId)
            .NotEmpty()
            .WithMessage("Item Id is required.");

        RuleFor(x => x.FinalPoint)
            .NotNull()
            .WithMessage("Final Point is required.")
            .GreaterThanOrEqualTo(0)
            .WithMessage("Final Point cannot be negative.");
    }
}

public class UpdateAuditTaskFinalPointHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAuditTaskFinalPointCommand>
{
    public async Task Handle(UpdateAuditTaskFinalPointCommand request, CancellationToken ct)
    {
        var task = await AuditTaskQueries.VisibleTo(db, user)
                .FirstOrDefaultAsync(row => row.Id == request.Id, ct)
            ?? throw new NotFoundException("Checklist item not found.");

        if (task.Status != AuditTaskStatus.Completed)
        {
            throw new ConflictException(
                "Final Review is only available after the audit is completed.");
        }

        var item = await db.AuditTaskItems
            .FirstOrDefaultAsync(row =>
                row.Id == request.ItemId
                && row.AuditTaskId == task.Id
                && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Checklist item not found.");

        item.FinalPoint = request.FinalPoint;

        // Tracked read: the edited row's new value must count towards the recomputed
        // score before SaveChanges flushes it.
        var items = await db.AuditTaskItems
            .Where(row => row.AuditTaskId == task.Id)
            .ToListAsync(ct);
        task.FinalReviewScore = AuditTaskQueries.Score(items, row => row.FinalPoint);

        await db.SaveChangesAsync(ct);
    }
}
