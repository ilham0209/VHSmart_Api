using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Supporting Document CRUD + template (spec 5.5). [HasPermission] gates every action on the
// Admin.SupportingDocuments screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 3.3) and the sequence rule (D-16). The template travels with the form - it is part of
// the spec 5.5 form, so create and update are multipart (optional file: only SOP views take
// one, and an omitted file keeps the current template).
[ApiController]
[Route("api/admin/supporting-documents")]
[Authorize]
public class SupportingDocumentController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllSupportingDocumentsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before "{id:guid}" - also unreachable as an id thanks to the guid constraint.
    [HttpGet("{id:guid}/template")]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.View)]
    public async Task<IActionResult> GetTemplate(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetSupportingDocumentTemplateQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSupportingDocumentByIdQuery(id), ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromForm] CreateSupportingDocumentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] UpdateSupportingDocumentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminSupportingDocuments, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteSupportingDocumentCommand(id), ct);
        return NoContent();
    }
}
