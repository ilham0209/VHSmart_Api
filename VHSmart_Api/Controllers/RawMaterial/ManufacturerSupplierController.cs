using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.RawMaterial;

// Manufacturer & Supplier CRUD + logo (spec 10.1). [HasPermission] gates every action on the
// RawMaterial.ManufacturerSupplier screen key (CodingRules 8.2); the handlers apply the tenant
// scope (spec 3.3) and the per-company e-mail rule (spec 21.9).
[ApiController]
[Route("api/raw-material/manufacturer-suppliers")]
[Authorize]
public class ManufacturerSupplierController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllManufacturerSuppliersQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}/logo")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.View)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetManufacturerSupplierLogoQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetManufacturerSupplierByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateManufacturerSupplierCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // Bulk upload (spec 10.1, 21.10): .xls/.xlsx, max 250 rows, partial success with the
    // per-row error list.
    [HttpPost("bulk-upload")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.Create)]
    public async Task<IActionResult> BulkUpload(
        [FromForm] BulkUploadManufacturerSuppliersCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // The logo belongs to an existing row, so it is an Edit of that row.
    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.Edit)]
    public async Task<IActionResult> UploadLogo(
        Guid id,
        [FromForm] UploadManufacturerSupplierLogoCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateManufacturerSupplierCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialManufacturerSupplier, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteManufacturerSupplierCommand(id), ct);
        return NoContent();
    }
}
