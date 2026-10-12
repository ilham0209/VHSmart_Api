using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Sequences;

namespace VHSmart_Api.Features.Audit.AuditPlanning;

// Create a planned audit from the clicked calendar date (spec 14.8 [CONFIRMED / MANUAL]):
// Audit Purpose*, Audit Type*, Category*, Details* (the premise - the manual screenshot
// shows Category = PREMISE and Details = the premise name), Schedule Date (the clicked
// date) and Assign by Group* (only Active groups - [CONFIRMED]). Template Selection is not
// a user choice: each group carries exactly one checklist (AU-06 radio), so the server
// copies the group's ChecklistId onto the plan and the modal only displays it. On save the
// system generates the Audit Reference No. (D-09 via IReferenceNumberGenerator) and the
// Audit Task is auto-created with status Not Started (spec 14.9 flow step 1). Audit
// Purpose/Type/Category come from General Data per the spec 14.1 probable mapping
// (Purpose <- "Audit Purpose", Type <- "Audit Type", Category <- "Internal - Audit
// Category" - flagged, the same [VERIFY] AU-04/AU-05 carry). Past dates and same-day
// duplicates are allowed: the spec does not rule them ([VERIFY], not invented). No
// notification (spec 14.8 "notification to the auditors" is unknown; D-25 sends only when
// a task says so).
public record CreateAuditPlanCommand(
    Guid AuditPurposeId,
    Guid AuditTypeId,
    Guid CategoryId,
    Guid PremiseId,
    DateOnly ScheduleDate,
    Guid GroupAuditorId) : IRequest<AuditPlanResponse>;

public record AuditPlanResponse(
    Guid Id,
    string AuditReferenceNo,
    Guid AuditPurposeId,
    Guid AuditTypeId,
    Guid CategoryId,
    Guid PremiseId,
    DateOnly ScheduleDate,
    Guid GroupAuditorId,
    Guid ChecklistId,
    Guid AssignedByUserId,
    DateOnly DateAssigned,
    Guid AuditTaskId);

public class CreateAuditPlanValidator : AbstractValidator<CreateAuditPlanCommand>
{
    // The General Data pairs of the spec 14.1 probable mapping (flagged).
    public const string AuditPurposeCategory = "Audit Purpose";
    public const string AuditTypeCategory = "Audit Type";
    public const string AuditCategory = "Internal - Audit Category";

    public CreateAuditPlanValidator(VHSmartDbContext db, ICurrentUser user)
    {
        RuleFor(x => x.AuditPurposeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Audit Purpose is required.")
            .MustAsync((id, ct) => IsGeneralDataValueAsync(
                db, user.CompanyId, AuditPurposeCategory, id, ct))
            .WithMessage("Audit Purpose not found.");

        RuleFor(x => x.AuditTypeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Audit Type is required.")
            .MustAsync((id, ct) => IsGeneralDataValueAsync(
                db, user.CompanyId, AuditTypeCategory, id, ct))
            .WithMessage("Audit Type not found.");

        RuleFor(x => x.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MustAsync((id, ct) => IsGeneralDataValueAsync(
                db, user.CompanyId, AuditCategory, id, ct))
            .WithMessage("Category not found.");

        RuleFor(x => x.PremiseId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Premise is required.")
            .MustAsync((id, ct) => db.Premises.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Premise not found.");

        RuleFor(x => x.ScheduleDate)
            .Must(value => value != default)
            .WithMessage("Schedule Date is required.");

        // Only groups with status Active can be selected (spec 14.7/14.8 [CONFIRMED]).
        RuleFor(x => x.GroupAuditorId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Assign by Group is required.")
            .MustAsync((id, ct) => db.GroupAuditors.AnyAsync(
                row => row.Id == id && row.CompanyId == user.CompanyId, ct))
            .WithMessage("Group Auditor not found.")
            .MustAsync((id, ct) => db.GroupAuditors.AnyAsync(
                row => row.Id == id
                    && row.CompanyId == user.CompanyId
                    && row.Status == GroupAuditorStatus.Active, ct))
            .WithMessage("Group Auditor is not active.");
    }

    internal static Task<bool> IsGeneralDataValueAsync(
        VHSmartDbContext db,
        Guid companyId,
        string category,
        Guid id,
        CancellationToken ct) =>
        db.GeneralData.AnyAsync(
            row => row.Id == id
                && row.CompanyId == companyId
                && row.Group == GeneralDataGroup.AUDIT
                && row.Category == category,
            ct);
}

public class CreateAuditPlanHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IReferenceNumberGenerator referenceNumbers)
    : IRequestHandler<CreateAuditPlanCommand, AuditPlanResponse>
{
    public async Task<AuditPlanResponse> Handle(
        CreateAuditPlanCommand request,
        CancellationToken ct)
    {
        var premise = await db.Premises.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.PremiseId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Premise not found.");

        var group = await db.GroupAuditors.AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == request.GroupAuditorId && row.CompanyId == user.CompanyId, ct)
            ?? throw new NotFoundException("Group Auditor not found.");

        var auditPrefix = await ResolveAuditPrefixAsync(premise, ct);

        // D-09: the premise key is its Store Code, or its name when none.
        var premiseKey = string.IsNullOrWhiteSpace(premise.StoreCode)
            ? premise.Name
            : premise.StoreCode;

        var referenceNo = await referenceNumbers.NextAuditReferenceNumberAsync(
            auditPrefix, premiseKey, request.ScheduleDate, ct);

        var now = DateTime.UtcNow;
        var assignedBy = Guid.TryParse(user.UserId, out var parsed) ? parsed : Guid.Empty;

        var plan = new AuditPlanEntity
        {
            CompanyId = user.CompanyId,
            AuditReferenceNo = referenceNo,
            AuditPurposeId = request.AuditPurposeId,
            AuditTypeId = request.AuditTypeId,
            CategoryId = request.CategoryId,
            PremiseId = request.PremiseId,
            ScheduleDate = request.ScheduleDate.ToDateTime(TimeOnly.MinValue),
            GroupAuditorId = request.GroupAuditorId,
            ChecklistId = group.ChecklistId,
            AssignedByUserId = assignedBy,
            DateAssigned = now
        };
        db.AuditPlans.Add(plan);

        // Spec 14.9 flow step 1: saving the plan auto-creates the task for the assigned
        // group's auditors with status Not Started; scores stay 0.00 (D-06).
        var task = new AuditTaskEntity
        {
            CompanyId = user.CompanyId,
            AuditPlanId = plan.Id,
            Status = AuditTaskStatus.NotStarted,
            AuditScore = 0m,
            FinalReviewScore = 0m
        };
        db.AuditTasks.Add(task);

        await db.SaveChangesAsync(ct);

        return new AuditPlanResponse(
            plan.Id,
            plan.AuditReferenceNo,
            plan.AuditPurposeId,
            plan.AuditTypeId,
            plan.CategoryId,
            plan.PremiseId,
            request.ScheduleDate,
            plan.GroupAuditorId,
            plan.ChecklistId,
            plan.AssignedByUserId,
            DateOnly.FromDateTime(plan.DateAssigned),
            task.Id);
    }

    // D-09: "Prefix comes from the premise's Brand. If the Brand has no Audit Prefix ->
    // planning is rejected with 'Audit Prefix is not set for brand {name}'."
    private async Task<string> ResolveAuditPrefixAsync(
        PremiseEntity premise,
        CancellationToken ct)
    {
        if (premise.BrandId is null)
        {
            // The premise carries no brand at all - the D-09 message needs a brand name,
            // so this variant is ours (flagged).
            throw new BusinessRuleException(
                "Audit Prefix is not set for the premise's brand.");
        }

        var prefix = await db.AuditPrefixes.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && row.BrandId == premise.BrandId
                && !string.IsNullOrWhiteSpace(row.Prefix))
            .Select(row => new { row.Prefix })
            .FirstOrDefaultAsync(ct);

        if (prefix is null)
        {
            var brandName = await db.GeneralData.AsNoTracking()
                .Where(row => row.Id == premise.BrandId && row.CompanyId == user.CompanyId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(ct);
            throw new BusinessRuleException(
                $"Audit Prefix is not set for brand {brandName}.");
        }

        return prefix.Prefix;
    }
}
