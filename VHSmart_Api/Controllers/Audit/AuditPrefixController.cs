using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Audit Prefix CRUD (spec 14.2). [HasPermission] gates every action on the Audit.AuditPrefix
// screen key (CodingRules 8.2); the handlers apply the tenant scope (spec 14.0) and the
// one-prefix-per-brand unique rule (Database.md 14.2).
[ApiController]
[Route("api/audit/audit-prefix")]
[Authorize]
public class AuditPrefixController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllAuditPrefixesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before "{id:guid}" - also unreachable as an id thanks to the guid constraint.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditPrefixOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetAuditPrefixByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateAuditPrefixCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAuditPrefixCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditAuditPrefix, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteAuditPrefixCommand(id), ct);
        return NoContent();
    }
}
