using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.CompanyInformation.Ihc;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.CompanyInformation;

// Company > Internal Halal Committee (spec 7.6): the Organisation Chart table, the Minutes
// Meeting list and the meeting form with its Attachment tab - all on the
// Company.InternalHalalCommittee screen key (CodingRules 8.2). The meeting id appears in the
// route, never a company id: the handlers scope every row to the caller's own company. The
// committee-level "Attachments" tab of the spec is [VERIFY] and skipped (TaskList CI-04).
[ApiController]
[Route("api/company-information/internal-halal-committee")]
[Authorize]
public class IhcController(ISender sender) : ControllerBase
{
    // Literal routes before "{id:guid}" - also unreachable as ids thanks to the guid constraint.
    [HttpGet("organisation-chart")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.View)]
    public async Task<IActionResult> GetOrganisationChart(CancellationToken ct) =>
        Ok(await sender.Send(new GetOrganisationChartQuery(), ct));

    [HttpGet]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllMinutesMeetingsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpPost]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMinutesMeetingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMinutesMeetingByIdQuery(id), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMinutesMeetingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteMinutesMeetingCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.Create)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        [FromForm] UploadMinutesMeetingAttachmentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { MinutesMeetingId = id }, ct));

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/document")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.View)]
    public async Task<IActionResult> GetAttachmentDocument(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetMinutesMeetingAttachmentDocumentQuery(id, attachmentId), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(PermissionKeys.CompanyInternalHalalCommittee, PermissionAction.Delete)]
    public async Task<IActionResult> DeleteAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        await sender.Send(new DeleteMinutesMeetingAttachmentCommand(id, attachmentId), ct);
        return NoContent();
    }
}
