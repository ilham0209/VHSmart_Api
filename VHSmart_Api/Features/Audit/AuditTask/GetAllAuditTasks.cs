using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Audit.AuditTask;

// "My Audit Task" (spec 14.9 [MANUAL] "an assigned auditor can login and see the list"):
// a task is visible when the current user is a member of its plan's group - membership is
// how the assignment reaches the auditor (AU-06; the leader is a member too, D-10). No
// separate planner/owner rule exists in the spec (flagged). Action is client side (only
// the Id is needed). Status is an enum on the response, so the house string-only search
// cannot cover it (flagged). Default sort Audit Reference No. ascending - the first
// visible data column stance of AU-02..AU-06 (flagged).
public record GetAllAuditTasksQuery : IRequest<DataGridResponse<GetAllAuditTasksResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllAuditTasksResponse(
    Guid Id,
    string AuditReferenceNo,
    DateOnly DateAssigned,
    string AssignedBy,
    DateOnly ScheduleDate,
    string PremiseAudited,
    DateOnly? ActualAuditDate,
    TimeSpan? ActualTimeIn,
    TimeSpan? ActualTimeOut,
    string? ReportingTo,
    AuditTaskStatus Status);

public class GetAllAuditTasksHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllAuditTasksQuery, DataGridResponse<GetAllAuditTasksResponse>>
{
    public async Task<DataGridResponse<GetAllAuditTasksResponse>> Handle(
        GetAllAuditTasksQuery request,
        CancellationToken ct)
    {
        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort
            ? nameof(GetAllAuditTasksResponse.AuditReferenceNo)
            : request.Request.SortBy;
        var descending = !defaultSort && request.Request.SortDescending;

        return await (
                from task in AuditTaskQueries.VisibleTo(db, user)
                join plan in db.AuditPlans on task.AuditPlanId equals plan.Id
                join premise in db.Premises on plan.PremiseId equals premise.Id
                join assignedByUser in db.Users
                    on plan.AssignedByUserId equals assignedByUser.Id
                    into assignedByGroup
                from assignedByUser in assignedByGroup.DefaultIfEmpty()
                select new GetAllAuditTasksResponse(
                    task.Id,
                    plan.AuditReferenceNo,
                    DateOnly.FromDateTime(plan.DateAssigned),
                    assignedByUser == null ? string.Empty : assignedByUser.Name,
                    DateOnly.FromDateTime(plan.ScheduleDate),
                    premise.Name,
                    task.StartAuditDate == null
                        ? (DateOnly?)null
                        : DateOnly.FromDateTime(task.StartAuditDate.Value),
                    task.TimeIn,
                    task.TimeOut,
                    task.ReportingTo,
                    task.Status))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllAuditTasksResponse.AuditReferenceNo),
                nameof(GetAllAuditTasksResponse.AssignedBy),
                nameof(GetAllAuditTasksResponse.PremiseAudited),
                nameof(GetAllAuditTasksResponse.ReportingTo))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}

// Shared visibility rule and score maths of the Audit Task features (spec 14.9, D-06).
internal static class AuditTaskQueries
{
    // Spec 14.9 [MANUAL]: only the assigned group's members see the task.
    public static IQueryable<AuditTaskEntity> VisibleTo(VHSmartDbContext db, ICurrentUser user)
    {
        var userId = Guid.TryParse(user.UserId, out var parsed) ? parsed : Guid.Empty;
        return from task in db.AuditTasks
               join plan in db.AuditPlans on task.AuditPlanId equals plan.Id
               where task.CompanyId == user.CompanyId
                     && db.GroupAuditorMembers.Any(member =>
                         member.CompanyId == user.CompanyId
                         && member.GroupAuditorId == plan.GroupAuditorId
                         && member.UserId == userId)
               select task;
    }

    public static async Task<bool> IsMemberAsync(
        VHSmartDbContext db,
        ICurrentUser user,
        Guid groupAuditorId)
    {
        var userId = Guid.TryParse(user.UserId, out var parsed) ? parsed : Guid.Empty;
        return await db.GroupAuditorMembers.AnyAsync(member =>
            member.CompanyId == user.CompanyId
            && member.GroupAuditorId == groupAuditorId
            && member.UserId == userId);
    }

    // D-06: Score = Sum(selected points) / Sum(PotentialPoint) x 100, 2 decimals; 0.00
    // when the checklist carries no potential (guard). Unanswered rows count as 0.
    public static decimal Score(
        IReadOnlyList<AuditTaskItemEntity> items,
        Func<AuditTaskItemEntity, decimal?> selector)
    {
        var potential = items.Sum(item => item.PotentialPoint);
        if (potential <= 0)
        {
            return 0m;
        }

        var scored = items.Sum(item => selector(item) ?? 0m);
        return Math.Round(scored / potential * 100m, 2);
    }
}
