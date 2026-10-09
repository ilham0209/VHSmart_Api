using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.HalalApplication;

// My Application (spec 12.3 / 12.4 / 12.5): the list screen, the "+ New Application" flow
// (scheme picker -> survey form -> create) and the application screen itself - its header,
// Company Information and Additional Information saves plus the three read-only tab lists
// (Establishment / Product / Raw Material read live from the batch). [HasPermission] gates
// every action on the HalalApplication.MyApplication screen key (CodingRules 8.2) - View for
// reads, Create for the creation, Edit for the saves (the screen's Save buttons edit one
// application); the handlers apply the ownership check (CompanyId from the JWT).
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

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetApplicationByIdQuery(id), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateApplicationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}/company-information")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateCompanyInformation(
        Guid id,
        UpdateCompanyInformationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpPut("{id:guid}/additional-information")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateAdditionalInformation(
        Guid id,
        UpdateAdditionalInformationCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpGet("{id:guid}/establishments")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetEstablishments(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetApplicationEstablishmentsQuery(id), ct));

    [HttpGet("{id:guid}/products")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetProducts(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetApplicationProductsQuery(id), ct));

    [HttpGet("{id:guid}/raw-materials")]
    [HasPermission(PermissionKeys.HalalApplicationMyApplication, PermissionAction.View)]
    public async Task<IActionResult> GetRawMaterials(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetApplicationRawMaterialsQuery(id), ct));
}
