using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.RawMaterial;

// Raw Material Master List (spec 10.2): list + form CRUD, the form's dropdown sources and the
// toolbar's "Multiple Delete". [HasPermission] gates every action on the RawMaterial.MasterList
// screen key (CodingRules 8.2); the handlers apply the special "Accessible For" visibility rule
// (CodingRules 7.3) and the ownership check.
[ApiController]
[Route("api/raw-material/master-list")]
[Authorize]
public class RawMaterialController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllRawMaterialsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segments before {id:guid} so they never bind as an id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetRawMaterialOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetRawMaterialByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateRawMaterialCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPost("bulk-delete")]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.Delete)]
    public async Task<IActionResult> BulkDelete(
        BulkDeleteRawMaterialsCommand command,
        CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateRawMaterialCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.RawMaterialMasterList, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteRawMaterialCommand(id), ct);
        return NoContent();
    }
}
