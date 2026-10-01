using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Controllers.Personnel;

// Personnel > All Staff (spec 7.4): list, add/edit modal (typhoid + IHC conditional rules),
// photo and the Staff Attachment tab - all on the Personnel.AllStaff screen key
// (CodingRules 8.2). Staff id and attachment id appear in the route, never a company id:
// the handlers scope every row to the caller's own company.
[ApiController]
[Route("api/personnel/staff")]
[Authorize]
public class StaffController(ISender sender) : ControllerBase
{
    // Literal routes before "{id:guid}" - also unreachable as ids thanks to the guid constraint.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetStaffOptionsQuery(), ct));

    [HttpGet("attachments/{attachmentId:guid}/document")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetAttachmentDocument(Guid attachmentId, CancellationToken ct)
    {
        var response = await sender.Send(new GetStaffAttachmentDocumentQuery(attachmentId), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Delete)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId, CancellationToken ct)
    {
        await sender.Send(new DeleteStaffAttachmentCommand(attachmentId), ct);
        return NoContent();
    }

    [HttpGet]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllStaffQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpPost]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetStaffByIdQuery(id), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStaffCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteStaffCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/photo")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetStaffPhotoQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpPut("{id:guid}/photo")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Edit)]
    public async Task<IActionResult> UploadPhoto(
        Guid id,
        [FromForm] UploadStaffPhotoCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpGet("{id:guid}/attachments")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.View)]
    public async Task<IActionResult> GetAttachments(
        Guid id,
        [FromQuery] DataGridRequest request,
        CancellationToken ct) =>
        Ok(await sender.Send(new GetAllStaffAttachmentsQuery(id, request), ct));

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PersonnelAllStaff, PermissionAction.Create)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        [FromForm] UploadStaffAttachmentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { StaffId = id }, ct));
}
