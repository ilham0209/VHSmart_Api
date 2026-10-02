using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Personnel;

// Personnel > Internal Training (spec 7.5): the Training List, the "Manage Training" modal
// (fields + attendance) and the Training Module List - all on the Personnel.InternalTraining
// screen key (CodingRules 8.2; legacy granule GC15). The training id appears in the route,
// never a company id: the handlers scope every row to the caller's own company.
[ApiController]
[Route("api/personnel/trainings")]
[Authorize]
public class TrainingController(ISender sender) : ControllerBase
{
    // Literal routes before "{id:guid}" - also unreachable as ids thanks to the guid constraint.
    [HttpGet("staff-options")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.View)]
    public async Task<IActionResult> GetStaffOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetTrainingStaffOptionsQuery(), ct));

    [HttpGet]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllTrainingsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpPost]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTrainingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetTrainingByIdQuery(id), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTrainingCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPost("{id:guid}/modules")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.Create)]
    public async Task<IActionResult> AddModule(
        Guid id,
        [FromForm] AddTrainingModuleCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { TrainingId = id }, ct));

    [HttpGet("{id:guid}/modules/{moduleId:guid}/document")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.View)]
    public async Task<IActionResult> GetModuleDocument(
        Guid id,
        Guid moduleId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetTrainingModuleDocumentQuery(id, moduleId), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpDelete("{id:guid}/modules/{moduleId:guid}")]
    [HasPermission(PermissionKeys.PersonnelInternalTraining, PermissionAction.Delete)]
    public async Task<IActionResult> DeleteModule(
        Guid id,
        Guid moduleId,
        CancellationToken ct)
    {
        await sender.Send(new DeleteTrainingModuleCommand(id, moduleId), ct);
        return NoContent();
    }
}
