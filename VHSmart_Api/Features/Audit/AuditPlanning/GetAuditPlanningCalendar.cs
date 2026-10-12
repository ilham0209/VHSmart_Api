using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Audit.AuditPlanning;

// The calendar API of spec 14.8: month / week / day switch around an anchor date. Each
// planned audit appears as a bar on its Schedule Date with the premise name, its store
// code in brackets ("(-)" when none - client renders from the nullable StoreCode) and the
// group name (bar text composed client-side; this endpoint returns the parts). The range
// endpoints share one query builder with Download Audit Plan. Company scoped explicitly;
// behind Audit.AuditPlanning View.
public record GetAuditPlanningCalendarQuery(string View, DateOnly Date)
    : IRequest<AuditPlanningCalendarResponse>;

public record AuditPlanningCalendarResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AuditPlanningBar> Items);

public record AuditPlanningBar(
    Guid Id,
    DateOnly ScheduleDate,
    string PremiseName,
    string? StoreCode,
    string GroupName,
    string AuditReferenceNo);

public class GetAuditPlanningCalendarValidator : AbstractValidator<GetAuditPlanningCalendarQuery>
{
    public const string Month = "month";
    public const string Week = "week";
    public const string Day = "day";

    public GetAuditPlanningCalendarValidator()
    {
        RuleFor(x => x.View)
            .Must(view => view is Month or Week or Day)
            .WithMessage("View must be month, week or day.");

        RuleFor(x => x.Date)
            .Must(value => value != default)
            .WithMessage("Date is required.");
    }
}

public class GetAuditPlanningCalendarHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAuditPlanningCalendarQuery, AuditPlanningCalendarResponse>
{
    public async Task<AuditPlanningCalendarResponse> Handle(
        GetAuditPlanningCalendarQuery request,
        CancellationToken ct)
    {
        var (rangeFrom, rangeTo) = AuditPlanningRange.Resolve(request.View, request.Date);

        var items = await (
            from plan in db.AuditPlans.AsNoTracking()
            where plan.CompanyId == user.CompanyId
                && plan.ScheduleDate >= rangeFrom.ToDateTime(TimeOnly.MinValue)
                && plan.ScheduleDate <= rangeTo.ToDateTime(TimeOnly.MinValue)
            orderby plan.ScheduleDate, plan.AuditReferenceNo
            select new AuditPlanningBar(
                plan.Id,
                DateOnly.FromDateTime(plan.ScheduleDate),
                (from premise in db.Premises
                 where premise.Id == plan.PremiseId
                 select premise.Name).FirstOrDefault() ?? string.Empty,
                (from premise in db.Premises
                 where premise.Id == plan.PremiseId
                 select premise.StoreCode).FirstOrDefault(),
                (from auditorGroup in db.GroupAuditors
                 where auditorGroup.Id == plan.GroupAuditorId
                 select auditorGroup.Name).FirstOrDefault() ?? string.Empty,
                plan.AuditReferenceNo))
            .ToListAsync(ct);

        return new AuditPlanningCalendarResponse(rangeFrom, rangeTo, items);
    }
}

// Shared range maths with Download Audit Plan: month = the anchor's calendar month, week
// = the anchor's ISO week (Monday - Sunday), day = the anchor date itself.
internal static class AuditPlanningRange
{
    public static (DateOnly From, DateOnly To) Resolve(string view, DateOnly date) =>
        view switch
        {
            GetAuditPlanningCalendarValidator.Week => (StartOfWeek(date), StartOfWeek(date).AddDays(6)),
            GetAuditPlanningCalendarValidator.Day => (date, date),
            _ => (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 1).AddMonths(1).AddDays(-1))
        };

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }
}
