using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.AuditChecklist;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Manage Audit Checklist CRUD (spec 14.6). [HasPermission] gates every action on the
// Audit.AuditChecklist screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 14.0). The two-step save of spec 14.6 [MANUAL] maps to POST (header only) then PUT
// (header + Criteria Selection in one save). "criteria-options" serves the modal's Criteria
// Selection table behind this screen's own View key so the feature needs no
// Audit.AuditCriteria permission (the GetAuditPrefixOptions reasoning).
[ApiController]
[Route("api/audit/audit-checklist")]
[Authorize]
public class AuditChecklistController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllAuditChecklistsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditChecklistOptionsQuery(), ct));

    [HttpGet("criteria-options")]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.View)]
    public async Task<IActionResult> GetCriteriaOptions(
        [FromQuery] GetAuditChecklistCriteriaOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditChecklistByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateAuditChecklistCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAuditChecklistCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditChecklist, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteAuditChecklistCommand(id), ct);
        return NoContent();
    }
}
