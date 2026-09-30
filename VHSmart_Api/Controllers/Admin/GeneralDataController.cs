using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// General Data CRUD (spec 5.1). [HasPermission] gates every action on the Admin.GeneralData
// screen key (CodingRules 8.2); the handlers apply the tenant scope (spec 3.3) and the
// duplicate rule (spec 21.9).
[ApiController]
[Route("api/admin/general-data")]
[Authorize]
public class GeneralDataController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllGeneralDataQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before "{id:guid}" - also unreachable as an id thanks to the guid constraint.
    [HttpGet("catalog")]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.View)]
    public async Task<IActionResult> GetCatalog(CancellationToken ct) =>
        Ok(await sender.Send(new GetGeneralDataCatalogQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetGeneralDataByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateGeneralDataCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateGeneralDataCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminGeneralData, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteGeneralDataCommand(id), ct);
        return NoContent();
    }
}
