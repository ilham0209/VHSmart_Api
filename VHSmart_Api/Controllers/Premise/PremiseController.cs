using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Premise;

// Premise > Manage Premise (spec 7.7): the Premise List (with the Premise Type filter), the
// "View Premise / Edit Premise" modal (Premise Information, Staff Information, Facility
// Information tabs), the Premise Attachment tab (list / upload / download - PDF only, D-22),
// the read-only Halal / Menu / Product information tabs (PR-04) and Update Premise Tag - all
// on the Premise.ManagePremise screen key (CodingRules 8.2;
// legacy granules C1 view / C3 edit / C4 delete). The premise id appears in the route, never
// a company id: the handlers scope every row to the caller's own company.
[ApiController]
[Route("api/premise/manage-premise")]
[Authorize]
public class PremiseController(ISender sender) : ControllerBase
{
    // Literal routes before "{id:guid}" - also unreachable as ids thanks to the guid constraint.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseOptionsQuery(), ct));

    [HttpGet]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllPremisesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpPost]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePremiseCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // Bulk upload (spec 7.7, 21.10): .xls/.xlsx, max 250 rows, partial success with the
    // per-row error list.
    [HttpPost("bulk-upload")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Create)]
    public async Task<IActionResult> BulkUpload(
        [FromForm] BulkUploadPremisesCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseByIdQuery(id), ct));

    // Premise Attachment tab (spec 7.7): the five D-15 types in order, N/A rows for the
    // untouched ones.
    [HttpGet("{id:guid}/attachments")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseAttachmentsQuery(id), ct));

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Create)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        [FromForm] UploadPremiseAttachmentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { PremiseId = id }, ct));

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/document")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetAttachmentDocument(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetPremiseAttachmentDocumentQuery(id, attachmentId), ct);
        return File(response.Content, response.ContentType);
    }

    // Halal Information tab (spec 7.7): one row per application of this premise, newest
    // first; the list above shows the newest one's columns.
    [HttpGet("{id:guid}/halal-information")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetHalalInformation(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseHalalInformationQuery(id), ct));

    // Menu/Product Information tabs (spec 7.7): Current and Approved are the same rows
    // behind two endpoints rather than a query flag, so each action maps to one tab.
    [HttpGet("{id:guid}/menu-information")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetMenuInformation(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(
            new GetPremiseMenuInformationQuery(id, Approved: false), ct));

    [HttpGet("{id:guid}/approved-menu-information")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetApprovedMenuInformation(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(
            new GetPremiseMenuInformationQuery(id, Approved: true), ct));

    [HttpGet("{id:guid}/product-information")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetProductInformation(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(
            new GetPremiseProductInformationQuery(id, Approved: false), ct));

    [HttpGet("{id:guid}/approved-product-information")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetApprovedProductInformation(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(
            new GetPremiseProductInformationQuery(id, Approved: true), ct));

    // "Download Halal Certificate" of the Menu/Product tab rows: the raw material's stored
    // HALAL CERTIFICATE file, validated against the premise's own tab scope (404 otherwise).
    [HttpGet("{id:guid}/menu-information/{menuId:guid}/certificate/{rawMaterialId:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetMenuCertificate(
        Guid id,
        Guid menuId,
        Guid rawMaterialId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetPremiseMenuCertificateQuery(id, menuId, rawMaterialId), ct);
        return File(response.Content, response.ContentType);
    }

    [HttpGet("{id:guid}/product-information/{productId:guid}/certificate/{rawMaterialId:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetProductCertificate(
        Guid id,
        Guid productId,
        Guid rawMaterialId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetPremiseProductCertificateQuery(id, productId, rawMaterialId), ct);
        return File(response.Content, response.ContentType);
    }

    [HttpPut("{id:guid}/tag")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateTag(
        Guid id,
        [FromBody] UpdatePremiseTagCommand command,
        CancellationToken ct)
    {
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePremiseCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeletePremiseCommand(id), ct);
        return NoContent();
    }
}
