using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.HalalCertificate;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.HalalApplication;

// Halal Application > Halal Certificate > Halal Certificate (spec 12.8): the "List of Halal
// Certificate" grid (item count + derived Valid/Expired), its detail, the edit save and the
// step-5 document upload / download. [HasPermission] gates every action on the
// HalalApplication.HalalCertificate screen key (CodingRules 8.2) - View for reads, Create
// for the upload (the screen's upload creates the document), Edit for the save; the
// handlers apply the ownership check (CompanyId from the JWT).
[ApiController]
[Route("api/halal-application/halal-certificates")]
[Authorize]
public class HalalCertificateController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.HalalApplicationHalalCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetHalalCertificatesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationHalalCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetHalalCertificateByIdQuery(id), ct));

    // Edit save: Certificate No. + the two dates (the spec never captured the legacy
    // dialog's fields - Database.md columns only, flagged).
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationHalalCertificate, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateHalalCertificateCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    // Manual flow step 5: Click Upload, choose the file (D-22 checked server side).
    [HttpPost("{id:guid}/document")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.HalalApplicationHalalCertificate, PermissionAction.Create)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] UploadHalalCertificateDocumentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { CertificateId = id }, ct));

    [HttpGet("{id:guid}/document")]
    [HasPermission(PermissionKeys.HalalApplicationHalalCertificate, PermissionAction.View)]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetHalalCertificateDocumentQuery(id), ct);
        return File(response.Content, response.ContentType);
    }
}
