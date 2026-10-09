using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.Finding;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Finding CRUD (spec 14.4). [HasPermission] gates every action on the Audit.Finding screen
// key (CodingRules 8.2); the handlers apply the tenant scope (spec 14.0 - each company's
// super admin maintains their own audit setup data). The recommendation-options endpoint
// serves the modal's checkbox table (search + paging) behind the same screen key so the
// screen works without the Audit.Recommendation key (the GetAuditPrefixOptions reasoning).
[ApiController]
[Route("api/audit/finding")]
[Authorize]
public class FindingController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllFindingsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("recommendation-options")]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.View)]
    public async Task<IActionResult> GetRecommendationOptions(
        [FromQuery] GetFindingRecommendationOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetFindingByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateFindingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateFindingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditFinding, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteFindingCommand(id), ct);
        return NoContent();
    }
}
