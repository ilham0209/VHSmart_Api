using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.GroupAuditor;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Manage Group Auditor CRUD (spec 14.7). [HasPermission] gates every action on the
// Audit.GroupAuditor screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 14.0). "options" serves both pickers of the add/edit modal (templates + auditors)
// behind this screen's own View key so the feature needs no other menu's permission.
[ApiController]
[Route("api/audit/group-auditor")]
[Authorize]
public class GroupAuditorController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllGroupAuditorsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetGroupAuditorOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetGroupAuditorByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateGroupAuditorCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateGroupAuditorCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditGroupAuditor, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteGroupAuditorCommand(id), ct);
        return NoContent();
    }
}
