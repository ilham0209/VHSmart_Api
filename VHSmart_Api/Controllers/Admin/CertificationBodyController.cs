using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.CertificationBodies;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Certification Bodies CRUD + logo (spec 5.2). [HasPermission] gates every action on the
// Admin.CertificationBodies screen key (CodingRules 8.2); each handler additionally requires
// IsPlatformAdmin - the screen belongs to the Serunai Super User only (D-07).
[ApiController]
[Route("api/admin/certification-bodies")]
[Authorize]
public class CertificationBodyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllCertificationBodiesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}/logo")]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.View)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetCertificationBodyLogoQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCertificationBodyByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateCertificationBodyCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // The logo belongs to an existing row, so it is an Edit of that row.
    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.Edit)]
    public async Task<IActionResult> UploadLogo(
        Guid id,
        [FromForm] UploadCertificationBodyLogoCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCertificationBodyCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminCertificationBodies, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteCertificationBodyCommand(id), ct);
        return NoContent();
    }
}
