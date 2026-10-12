using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.AuditTask;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Audit Task of spec 14.9: "My Audit Task" list + detail (header, checklist snapshot),
// Start Audit, the row answers, Acknowledgement + Submit Report and the post-completion
// Final Point (D-06). Visibility is the group-membership rule of the handlers; the
// controller only applies the screen permission (CodingRules 8.2) and binds route ids
// into the commands (house pattern).
[ApiController]
[Route("api/audit/audit-task")]
[Authorize]
public class AuditTaskController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllAuditTasksQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditTaskByIdQuery(id), ct));

    [HttpGet("{id:guid}/items")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.View)]
    public async Task<IActionResult> GetItems(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditTaskItemsQuery(id), ct));

    [HttpPost("{id:guid}/start")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.Edit)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await sender.Send(new StartAuditAuditTaskCommand(id), ct);
        return NoContent();
    }

    [HttpPut("items/{itemId:guid}")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateItem(
        Guid itemId,
        UpdateAuditTaskItemCommand command,
        CancellationToken ct)
    {
        await sender.Send(command with { Id = itemId }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.Edit)]
    public async Task<IActionResult> Submit(
        Guid id,
        SubmitAuditTaskCommand command,
        CancellationToken ct)
    {
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    // Final Review (D-06): Completed audits only - the one write past the Submit lock.
    [HttpPut("{id:guid}/items/{itemId:guid}/final-point")]
    [HasPermission(PermissionKeys.AuditAuditTask, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateFinalPoint(
        Guid id,
        Guid itemId,
        UpdateAuditTaskFinalPointCommand command,
        CancellationToken ct)
    {
        await sender.Send(command with { Id = id, ItemId = itemId }, ct);
        return NoContent();
    }
}
