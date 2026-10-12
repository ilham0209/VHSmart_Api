using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// Answer one checklist row (spec 14.9 flow step 3 / tab "Audit Checklist"): the auditor
// edits Actual Point (required - the score input of D-06) plus the optional Conformance
// flag and Remarks. How Conformance Yes/No is entered and what the red X mean is [VERIFY]
// in the spec, so IsConforming is a plain optional bool, never derived - the later NC
// feed (AU-10) will read it. Actual Point above Potential Point is not ruled out ([VERIFY],
// not invented). Only while the task is InProgress; Completed is locked ([CONFIRMED]),
// NotStarted is a 409 (start first). Final Point is NOT editable here - D-06 gives it to
// the post-completion Final Review endpoint.
public record UpdateAuditTaskItemCommand(
    Guid Id,
    decimal? ActualPoint,
    bool? IsConforming,
    string? Remarks) : IRequest;

public class UpdateAuditTaskItemValidator : AbstractValidator<UpdateAuditTaskItemCommand>
{
    public UpdateAuditTaskItemValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.ActualPoint)
            .NotNull()
            .WithMessage("Actual Point is required.")
            .GreaterThanOrEqualTo(0)
            .WithMessage("Actual Point cannot be negative.");

        RuleFor(x => x.Remarks)
            .MaximumLength(1000)
            .WithMessage("Remarks cannot exceed 1000 characters.");
    }
}

public class UpdateAuditTaskItemHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateAuditTaskItemCommand>
{
    public async Task Handle(UpdateAuditTaskItemCommand request, CancellationToken ct)
    {
        var item = await db.AuditTaskItems
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Checklist item not found.");

        var task = await db.AuditTasks
            .FirstOrDefaultAsync(
                row => row.Id == item.AuditTaskId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Checklist item not found.");

        var plan = await db.AuditPlans.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == task.AuditPlanId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Checklist item not found.");

        if (!await AuditTaskQueries.IsMemberAsync(db, user, plan.GroupAuditorId))
        {
            throw new NotFoundException("Checklist item not found.");
        }

        if (task.Status == AuditTaskStatus.NotStarted)
        {
            throw new ConflictException("The audit has not been started yet.");
        }

        if (task.Status == AuditTaskStatus.Completed)
        {
            throw new ConflictException("The audit is completed and can no longer be edited.");
        }

        item.ActualPoint = request.ActualPoint;
        item.IsConforming = request.IsConforming;
        item.Remarks = request.Remarks;

        await db.SaveChangesAsync(ct);
    }
}
