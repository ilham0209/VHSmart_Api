using System.Globalization;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPlanning;

// "Download Audit Plan" (spec 14.8, top-right of the calendar): the file format and content
// were not seen ([CONFIRMED / VERIFY]) - the task row allows JSON or CSV, so this returns
// a CSV of the SAME range the calendar shows (view + anchor date; flagged). Content =
// one row per plan with the reference number, schedule date, premise, store code, group,
// the three General Data labels, the audit template, the planner and the assigned date.
// Behind Audit.AuditPlanning View (an export stays inside the screen's own permission;
// legacy unauthenticated exports are a known defect - spec 23 - never copied).
public record DownloadAuditPlanQuery(string View, DateOnly Date) : IRequest<DownloadedAuditPlan>;

public record DownloadedAuditPlan(string FileName, string Content);

public class DownloadAuditPlanValidator : AbstractValidator<DownloadAuditPlanQuery>
{
    public DownloadAuditPlanValidator()
    {
        RuleFor(x => x.View)
            .Must(view => view is GetAuditPlanningCalendarValidator.Month
                or GetAuditPlanningCalendarValidator.Week
                or GetAuditPlanningCalendarValidator.Day)
            .WithMessage("View must be month, week or day.");

        RuleFor(x => x.Date)
            .Must(value => value != default)
            .WithMessage("Date is required.");
    }
}

public class DownloadAuditPlanHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<DownloadAuditPlanQuery, DownloadedAuditPlan>
{
    public async Task<DownloadedAuditPlan> Handle(
        DownloadAuditPlanQuery request,
        CancellationToken ct)
    {
        var (rangeFrom, rangeTo) = AuditPlanningRange.Resolve(request.View, request.Date);

        var rows = await (
            from plan in db.AuditPlans.AsNoTracking()
            where plan.CompanyId == user.CompanyId
                && plan.ScheduleDate >= rangeFrom.ToDateTime(TimeOnly.MinValue)
                && plan.ScheduleDate <= rangeTo.ToDateTime(TimeOnly.MinValue)
            orderby plan.ScheduleDate, plan.AuditReferenceNo
            select new
            {
                plan.AuditReferenceNo,
                plan.ScheduleDate,
                PremiseName = (from premise in db.Premises
                               where premise.Id == plan.PremiseId
                               select premise.Name).FirstOrDefault() ?? string.Empty,
                StoreCode = (from premise in db.Premises
                             where premise.Id == plan.PremiseId
                             select premise.StoreCode).FirstOrDefault(),
                GroupName = (from auditorGroup in db.GroupAuditors
                             where auditorGroup.Id == plan.GroupAuditorId
                             select auditorGroup.Name).FirstOrDefault() ?? string.Empty,
                AuditPurpose = (from general in db.GeneralData
                                where general.Id == plan.AuditPurposeId
                                select general.Name).FirstOrDefault() ?? string.Empty,
                AuditType = (from general in db.GeneralData
                             where general.Id == plan.AuditTypeId
                             select general.Name).FirstOrDefault() ?? string.Empty,
                Category = (from general in db.GeneralData
                            where general.Id == plan.CategoryId
                            select general.Name).FirstOrDefault() ?? string.Empty,
                Template = (from checklist in db.AuditChecklists
                            where checklist.Id == plan.ChecklistId
                            select checklist.Name).FirstOrDefault() ?? string.Empty,
                AssignedBy = (from assigned in db.Users
                              where assigned.Id == plan.AssignedByUserId
                              select assigned.Name).FirstOrDefault() ?? string.Empty,
                plan.DateAssigned
            })
            .ToListAsync(ct);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", new[]
        {
            "Audit Reference No",
            "Schedule Date",
            "Premise",
            "Store Code",
            "Group",
            "Audit Purpose",
            "Audit Type",
            "Category",
            "Audit Template",
            "Assigned By",
            "Date Assigned"
        }));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Escape(row.AuditReferenceNo),
                row.ScheduleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(row.PremiseName),
                Escape(row.StoreCode ?? string.Empty),
                Escape(row.GroupName),
                Escape(row.AuditPurpose),
                Escape(row.AuditType),
                Escape(row.Category),
                Escape(row.Template),
                Escape(row.AssignedBy),
                row.DateAssigned.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }));
        }

        var fileName = $"audit-plan-{request.View}-{request.Date:yyyy-MM-dd}.csv";
        return new DownloadedAuditPlan(fileName, builder.ToString());
    }

    // RFC 4180: quote fields containing a comma, quote or line break; double the quotes.
    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
