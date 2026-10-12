using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.NonConformanceList;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Non Conformance List (spec 14.11): the list of generated NCs, the Manage Corrective
// Action Details edit page and its Corrective Action History. [HasPermission] gates every
// action on the Audit.NonConformance screen key (CodingRules 8.2); the handlers apply the
// tenant scope (spec 14.0). There is no Create/Delete on this screen (the spec Action
// column is view + edit only) - internal NCs arrive from the audit-task feed and external
// ones from the report's NC tab (AU-09).
[ApiController]
[Route("api/audit/non-conformance")]
[Authorize]
public class NonConformanceController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditNonConformance, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllNonConformancesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditNonConformance, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetNonConformanceByIdQuery(id), ct));

    [HttpGet("{id:guid}/corrective-action-history")]
    [HasPermission(PermissionKeys.AuditNonConformance, PermissionAction.View)]
    public async Task<IActionResult> GetCorrectiveActionHistory(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCorrectiveActionHistoryQuery(id), ct));

    [HttpPost("{id:guid}/corrective-action")]
    [HasPermission(PermissionKeys.AuditNonConformance, PermissionAction.Edit)]
    public async Task<IActionResult> SaveCorrectiveAction(
        Guid id,
        [FromForm] SaveCorrectiveActionCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}/review")]
    [HasPermission(PermissionKeys.AuditNonConformance, PermissionAction.Edit)]
    public async Task<IActionResult> ReviewCorrectiveAction(
        Guid id,
        ReviewCorrectiveActionCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));
}
