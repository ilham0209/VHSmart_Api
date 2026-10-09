using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.Recommendation;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Recommendation CRUD (spec 14.3). [HasPermission] gates every action on the
// Audit.Recommendation screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 14.0 - each company's super admin maintains their own audit setup data).
[ApiController]
[Route("api/audit/recommendation")]
[Authorize]
public class RecommendationController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllRecommendationsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetRecommendationByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateRecommendationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateRecommendationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteRecommendationCommand(id), ct);
        return NoContent();
    }
}
