using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// Start Audit (spec 14.9 flow step 2 [CONFIRMED]): NotStarted -> InProgress, the Start
// Audit Date + Time In are recorded (server UTC clock - the spec does not say server vs
// typed [VERIFY]; D-24 stores UTC and the KL display is the client's job, flagged), and
// the checklist is SNAPSHOT into AudAuditTaskItems "so later edits never change past
// audits" (Database.md 13) - Category/Criteria/SubCriteria/Reference/PotentialPoint are
// frozen text/points from the plan's linked checklist at this moment; soft-deleted source
// rows drop out via the global filter. A second start is a 409 (one-way lifecycle). No
// notification (D-25).
public record StartAuditAuditTaskCommand(Guid Id) : IRequest;

public class StartAuditAuditTaskValidator : AbstractValidator<StartAuditAuditTaskCommand>
{
    public StartAuditAuditTaskValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class StartAuditAuditTaskHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<StartAuditAuditTaskCommand>
{
    public async Task Handle(StartAuditAuditTaskCommand request, CancellationToken ct)
    {
        var task = await db.AuditTasks
            .FirstOrDefaultAsync(
                row => row.Id == request.Id && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Audit task not found.");

        var plan = await db.AuditPlans.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == task.AuditPlanId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Audit task not found.");

        // Unknown and foreign are both 404 (house rule) - a non-member never learns that
        // the task exists.
        if (!await AuditTaskQueries.IsMemberAsync(db, user, plan.GroupAuditorId))
        {
            throw new NotFoundException("Audit task not found.");
        }

        if (task.Status != AuditTaskStatus.NotStarted)
        {
            throw new ConflictException("The audit has already been started.");
        }

        var now = DateTime.UtcNow;
        task.Status = AuditTaskStatus.InProgress;
        task.StartAuditDate = now.Date;
        task.TimeIn = now.TimeOfDay;

        var checklistId = plan.ChecklistId;
        var snapshotRows = await (
                from link in db.AuditChecklistCriteria.AsNoTracking()
                where link.CompanyId == user.CompanyId && link.ChecklistId == checklistId
                join criteria in db.AuditCriteria.AsNoTracking()
                    on link.AuditCriteriaId equals criteria.Id
                join category in db.GeneralData.AsNoTracking()
                    on criteria.CategoryId equals category.Id
                    into categoryGroup
                from category in categoryGroup.DefaultIfEmpty()
                join criteriaMaster in db.AuditCriteriaMasters.AsNoTracking()
                    on criteria.CriteriaId equals criteriaMaster.Id
                    into criteriaMasterGroup
                from criteriaMaster in criteriaMasterGroup.DefaultIfEmpty()
                join subCriteriaMaster in db.AuditCriteriaMasters.AsNoTracking()
                    on criteria.SubCriteriaId equals subCriteriaMaster.Id
                    into subCriteriaMasterGroup
                from subCriteriaMaster in subCriteriaMasterGroup.DefaultIfEmpty()
                orderby criteria.CategorySequence, criteria.CriteriaSequence
                select new
                {
                    criteria.Id,
                    Category = category == null ? string.Empty : category.Name,
                    criteria.CategorySequence,
                    Criteria = criteriaMaster == null ? string.Empty : criteriaMaster.Text,
                    criteria.CriteriaSequence,
                    SubCriteria = subCriteriaMaster == null
                        ? (string?)null
                        : subCriteriaMaster.Text,
                    criteria.Reference,
                    criteria.PotentialPoint
                })
            .ToListAsync(ct);

        foreach (var row in snapshotRows)
        {
            db.AuditTaskItems.Add(new AuditTaskItemEntity
            {
                CompanyId = user.CompanyId,
                AuditTaskId = task.Id,
                SourceAuditCriteriaId = row.Id,
                Category = row.Category,
                CategorySequence = row.CategorySequence,
                Criteria = row.Criteria,
                CriteriaSequence = row.CriteriaSequence,
                SubCriteria = row.SubCriteria,
                Reference = row.Reference,
                PotentialPoint = row.PotentialPoint
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
