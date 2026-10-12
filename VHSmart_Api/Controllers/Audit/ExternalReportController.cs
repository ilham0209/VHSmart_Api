using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Audit;

// Upload External Report (spec 14.10): the report CRUD-lite, the NC tab and the
// attachment tab. [HasPermission] gates every action on the Audit.ExternalReport screen
// key (CodingRules 8.2); the handlers apply the tenant scope (spec 14.0). The NC and
// attachment writes carry Edit: they belong to the manage form, not to a separate screen.
[ApiController]
[Route("api/audit/external-report")]
[Authorize]
public class ExternalReportController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllExternalAuditReportsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetExternalAuditReportOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetExternalAuditReportByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateExternalAuditReportCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateExternalAuditReportCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteExternalAuditReportCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/non-conformances")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.View)]
    public async Task<IActionResult> GetNonConformances(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetNonConformancesQuery(id), ct));

    [HttpPost("{id:guid}/non-conformances")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> CreateNonConformance(
        Guid id,
        CreateNonConformanceCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("non-conformances/{nonConformanceId:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateNonConformance(
        Guid nonConformanceId,
        UpdateNonConformanceCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = nonConformanceId }, ct));

    [HttpDelete("non-conformances/{nonConformanceId:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> DeleteNonConformance(
        Guid nonConformanceId,
        CancellationToken ct)
    {
        await sender.Send(new DeleteNonConformanceCommand(nonConformanceId), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/attachments")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.View)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetExternalAuditReportAttachmentsQuery(id), ct));

    [HttpPost("{id:guid}/attachments")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        [FromForm] UploadExternalAuditReportAttachmentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("attachments/{attachmentId:guid}")]
    [HasPermission(PermissionKeys.AuditExternalReport, PermissionAction.Edit)]
    public async Task<IActionResult> DeleteAttachment(
        Guid attachmentId,
        CancellationToken ct)
    {
        await sender.Send(new DeleteExternalAuditReportAttachmentCommand(attachmentId), ct);
        return NoContent();
    }
}
