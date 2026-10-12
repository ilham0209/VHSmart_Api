using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.AuditPlanning;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Manage Audit Planning CRUD-lite (spec 14.8): the calendar (month/week/day), the create
// modal's pickers, create-from-date (the system generates the Audit Reference No. and the
// Audit Task - edit/cancel of a planned audit is [VERIFY] and NOT in the task row's scope)
// and the CSV download of the visible range (format [VERIFY] - the task row allows
// JSON/CSV, flagged). [HasPermission] gates every action on the Audit.AuditPlanning screen
// key (CodingRules 8.2); the handlers apply the tenant scope (spec 14.0).
[ApiController]
[Route("api/audit/audit-planning")]
[Authorize]
public class AuditPlanningController(ISender sender) : ControllerBase
{
    [HttpGet("calendar")]
    [HasPermission(PermissionKeys.AuditAuditPlanning, PermissionAction.View)]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] GetAuditPlanningCalendarQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditAuditPlanning, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditPlanningOptionsQuery(), ct));

    [HttpGet("download")]
    [HasPermission(PermissionKeys.AuditAuditPlanning, PermissionAction.View)]
    public async Task<IActionResult> Download(
        [FromQuery] DownloadAuditPlanQuery query,
        CancellationToken ct)
    {
        var plan = await sender.Send(query, ct);
        return File(
            Encoding.UTF8.GetBytes(plan.Content),
            "text/csv",
            plan.FileName);
    }

    [HttpPost]
    [HasPermission(PermissionKeys.AuditAuditPlanning, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateAuditPlanCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));
}
