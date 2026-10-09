using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.HalalApplication;

// My Application (spec 12.3 / 12.4): the list screen, the "+ New Application" flow (scheme
// picker -> survey form -> create) and the survey form's own questions. [HasPermission]
// gates every action on the HalalApplication.MyApplication screen key (CodingRules 8.2);
// the handlers apply the ownership check (CompanyId from the JWT). The application screen
// itself (tabs, submit) is HA-03+ and has no endpoints here.
[ApiController]
[Route("api/halal-application/my-applications")]
[Authorize]
public class MyApplicationController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllApplicationsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segments, so they never bind as an id (there is no {id} route yet).
    [HttpGet("survey-questions")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetSurveyQuestions(CancellationToken ct) =>
        Ok(await sender.Send(new GetSurveyQuestionsQuery(), ct));

    [HttpGet("schemes")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetSchemes(CancellationToken ct) =>
        Ok(await sender.Send(new GetApplicationSchemesQuery(), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateApplicationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));
}
