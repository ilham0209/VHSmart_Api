using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Web Links CRUD + icon (spec 5.4). [HasPermission] gates every action on the Admin.WebLinks
// screen key (CodingRules 8.2); the handlers apply the tenant scope (spec 3.3) and the
// duplicate rule (spec 21.9). The icon travels with the form - Database.md 3 marks it required
// (Icon*), so create and update are multipart and never write a row without one.
[ApiController]
[Route("api/admin/web-links")]
[Authorize]
public class WebLinkController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllWebLinksQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before "{id:guid}" - also unreachable as an id thanks to the guid constraint.
    [HttpGet("{id:guid}/icon")]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.View)]
    public async Task<IActionResult> GetIcon(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetWebLinkIconQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetWebLinkByIdQuery(id), ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromForm] CreateWebLinkCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] UpdateWebLinkCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminWebLinks, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteWebLinkCommand(id), ct);
        return NoContent();
    }
}
