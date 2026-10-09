using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.AuditCriteria;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Manage Audit Criteria CRUD (spec 14.5). [HasPermission] gates every action on the
// Audit.AuditCriteria screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 14.0 - each company's super admin maintains their own audit setup data). The
// options endpoint carries the modal's dropdown sources and "masters" serves the green "+"
// inline create, both behind this screen's own key so the feature needs no other menu's
// permission (the GetAuditPrefixOptions reasoning).
[ApiController]
[Route("api/audit/audit-criteria")]
[Authorize]
public class AuditCriteriaController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllAuditCriteriaQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditCriteriaOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditCriteriaByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateAuditCriteriaCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPost("masters")]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.Create)]
    public async Task<IActionResult> CreateMaster(
        CreateAuditCriteriaMasterCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAuditCriteriaCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditCriteria, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteAuditCriteriaCommand(id), ct);
        return NoContent();
    }
}
