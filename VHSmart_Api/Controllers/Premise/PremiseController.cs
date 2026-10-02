using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Premise;

// Premise > Manage Premise (spec 7.7): the Premise List (with the Premise Type filter) and
// the "View Premise / Edit Premise" modal (Premise Information, Staff Information, Facility
// Information tabs) - all on the Premise.ManagePremise screen key (CodingRules 8.2; legacy
// granules C1 view / C3 edit / C4 delete). The premise id appears in the route, never a
// company id: the handlers scope every row to the caller's own company. The Premise
// Attachment and Product/Menu/Halal tabs are separate endpoints added by PR-02/PR-04.
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

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PremiseManagePremise, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseByIdQuery(id), ct));

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
