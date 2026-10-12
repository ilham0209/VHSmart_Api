using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditTask;

// The detail page header of spec 14.9 (read-only fields list + the button/permission
// states): reference/schedule/premise(+address), group with its members and leader, the
// template, the assigner and the date assigned, the Start/End dates + times and the two
// scores (0.00 until answered/submitted - D-06). The three Acknowledgement fields are
// returned too so the tab can round-trip (spec places them on the tab, flagged). Group
// members come back as a name list, alphabetical like AU-06 (the "Group Audit (members
// in brackets)" display composes client-side). Behind Audit.AuditTask View.
public record GetAuditTaskByIdQuery(Guid Id) : IRequest<GetAuditTaskByIdResponse>;

public record GetAuditTaskByIdResponse(
    Guid Id,
    string AuditReferenceNo,
    DateOnly ScheduleDate,
    Guid PremiseId,
    string PremiseName,
    string PremiseAddress,
    Guid GroupAuditorId,
    string GroupName,
    IReadOnlyList<string> GroupMemberNames,
    string GroupLeaderName,
    Guid ChecklistId,
    string ChecklistName,
    Guid AssignedByUserId,
    string AssignedByName,
    DateOnly DateAssigned,
    DateOnly? StartAuditDate,
    TimeSpan? TimeIn,
    DateOnly? EndAuditDate,
    TimeSpan? TimeOut,
    decimal AuditScore,
    decimal FinalReviewScore,
    string? ReportingTo,
    string? Summary,
    string? AuditeeName,
    string? AuditeeDesignation,
    AuditTaskStatus Status);

public class GetAuditTaskByIdValidator : AbstractValidator<GetAuditTaskByIdQuery>
{
    public GetAuditTaskByIdValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");
    }
}

public class GetAuditTaskByIdHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditTaskByIdQuery, GetAuditTaskByIdResponse>
{
    public async Task<GetAuditTaskByIdResponse> Handle(
        GetAuditTaskByIdQuery request,
        CancellationToken ct)
    {
        var task = await (
                from row in AuditTaskQueries.VisibleTo(db, user)
                join planRow in db.AuditPlans on row.AuditPlanId equals planRow.Id
                where row.Id == request.Id
                select new { row, plan = planRow })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Audit task not found.");

        var premise = await db.Premises.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == task.plan.PremiseId, ct);
        var checklistName = await db.AuditChecklists.AsNoTracking()
            .Where(row => row.Id == task.plan.ChecklistId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct);
        var group = await db.GroupAuditors.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == task.plan.GroupAuditorId, ct);
        var groupLeaderName = group == null
            ? string.Empty
            : await db.Users.AsNoTracking()
                  .Where(row => row.Id == group.LeaderUserId)
                  .Select(row => row.Name)
                  .FirstOrDefaultAsync(ct) ?? string.Empty;
        var memberNames = await (
            from member in db.GroupAuditorMembers.AsNoTracking()
            where member.CompanyId == user.CompanyId
                  && member.GroupAuditorId == task.plan.GroupAuditorId
            join memberUser in db.Users on member.UserId equals memberUser.Id
            orderby memberUser.Name
            select memberUser.Name).ToListAsync(ct);
        var assignedByName = await db.Users.AsNoTracking()
            .Where(row => row.Id == task.plan.AssignedByUserId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // The manual shows one Address field; Address2 is appended when present (flagged).
        var premiseAddress = premise == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(premise.Address2)
                ? premise.Address1
                : $"{premise.Address1}, {premise.Address2}";

        return new GetAuditTaskByIdResponse(
            task.row.Id,
            task.plan.AuditReferenceNo,
            DateOnly.FromDateTime(task.plan.ScheduleDate),
            task.plan.PremiseId,
            premise?.Name ?? string.Empty,
            premiseAddress,
            task.plan.GroupAuditorId,
            group?.Name ?? string.Empty,
            memberNames,
            groupLeaderName,
            task.plan.ChecklistId,
            checklistName ?? string.Empty,
            task.plan.AssignedByUserId,
            assignedByName,
            DateOnly.FromDateTime(task.plan.DateAssigned),
            task.row.StartAuditDate == null
                ? null
                : DateOnly.FromDateTime(task.row.StartAuditDate.Value),
            task.row.TimeIn,
            task.row.EndAuditDate == null
                ? null
                : DateOnly.FromDateTime(task.row.EndAuditDate.Value),
            task.row.TimeOut,
            task.row.AuditScore,
            task.row.FinalReviewScore,
            task.row.ReportingTo,
            task.row.Summary,
            task.row.AuditeeName,
            task.row.AuditeeDesignation,
            task.row.Status);
    }
}
