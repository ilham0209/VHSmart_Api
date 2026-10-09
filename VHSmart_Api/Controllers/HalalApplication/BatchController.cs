using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.HalalApplication;

// Manage Batch (spec 12.2): list + form CRUD, the form's dropdown sources and the edit
// modal's product / premise pickers with their link-unlink halves. [HasPermission] gates
// every action on the HalalApplication.ManageBatch screen key (CodingRules 8.2); the handlers
// apply the ownership check (CompanyId from the JWT).
[ApiController]
[Route("api/halal-application/manage-batches")]
[Authorize]
public class BatchController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllBatchesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segments before {id:guid} so they never bind as an id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetBatchOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetBatchByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateBatchCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateBatchCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteBatchCommand(id), ct);
        return NoContent();
    }

    // Edit modal product side (spec 12.2 flow 5): the pick source behind "Link", then the
    // link / unlink halves of the row's Action icon - Create / Delete, exactly how the
    // ingredient tab gates its own halves. Every handler enforces the batch mode (a Food
    // Premise batch answers 400) and D-18 on the product.
    [HttpGet("{id:guid}/products/options")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.View)]
    public async Task<IActionResult> GetProductOptions(
        Guid id,
        [FromQuery] GetBatchProductOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query with { BatchId = id }, ct));

    [HttpPost("{id:guid}/products")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Create)]
    public async Task<IActionResult> LinkProduct(
        Guid id,
        LinkBatchProductCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { BatchId = id }, ct));

    [HttpDelete("{id:guid}/products/{batchProductId:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Delete)]
    public async Task<IActionResult> UnlinkProduct(
        Guid id,
        Guid batchProductId,
        CancellationToken ct)
    {
        await sender.Send(new UnlinkBatchProductCommand(id, batchProductId), ct);
        return NoContent();
    }

    // Edit modal premise side (spec 12.2 flow 6, "Associate Premise"): D-15 complete-only
    // picker, link guard and the soft-delete unlink (AppBatchPremises has no MappingStatus).
    [HttpGet("{id:guid}/premises/options")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.View)]
    public async Task<IActionResult> GetPremiseOptions(
        Guid id,
        [FromQuery] GetBatchPremiseOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query with { BatchId = id }, ct));

    [HttpPost("{id:guid}/premises")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Create)]
    public async Task<IActionResult> LinkPremise(
        Guid id,
        LinkBatchPremiseCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { BatchId = id }, ct));

    [HttpDelete("{id:guid}/premises/{batchPremiseId:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationManageBatch, PermissionAction.Delete)]
    public async Task<IActionResult> UnlinkPremise(
        Guid id,
        Guid batchPremiseId,
        CancellationToken ct)
    {
        await sender.Send(new UnlinkBatchPremiseCommand(id, batchPremiseId), ct);
        return NoContent();
    }
}
