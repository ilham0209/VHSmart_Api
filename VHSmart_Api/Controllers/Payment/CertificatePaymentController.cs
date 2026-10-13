using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Payment.CertificatePayment;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Payment;

// Payment > Certificate (spec 15.1): the list with its View / Application Type filters, the
// form's application and staff lookups, the detail, the status-workflow save, soft delete
// and the attachment dialog. [HasPermission] gates every action on the Payment.Certificate
// screen key (CodingRules 8.2); the handlers apply the tenant scope. The literal
// application-lookup / staff-lookup segments sit above {id:guid} so they never bind as an
// id. The status workflow itself lives in PaymentStatusPolicy (D-23), not here.
[ApiController]
[Route("api/payment/certificate")]
[Authorize]
public class CertificatePaymentController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllCertificatePaymentsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("application-lookup")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetApplicationLookup(
        [FromQuery] GetCertificatePaymentApplicationLookupQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("staff-lookup")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetStaffLookup(
        [FromQuery] GetPaymentStaffOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCertificatePaymentByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateCertificatePaymentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // D-23: the save may also advance the status forward exactly one step.
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCertificatePaymentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteCertificatePaymentCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/attachments")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCertificatePaymentAttachmentsQuery(id), ct));

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.Edit)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        [FromForm] UploadCertificatePaymentAttachmentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/document")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetAttachmentDocument(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetCertificatePaymentAttachmentDocumentQuery(id, attachmentId), ct);
        return File(response.Content, response.ContentType);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(PermissionKeys.PaymentCertificate, PermissionAction.Edit)]
    public async Task<IActionResult> DeleteAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        await sender.Send(
            new DeleteCertificatePaymentAttachmentCommand(id, attachmentId), ct);
        return NoContent();
    }
}
