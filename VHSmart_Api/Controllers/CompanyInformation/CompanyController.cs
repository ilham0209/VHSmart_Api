using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.CompanyInformation.General;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.CompanyInformation;

// Company > General (spec 7.1): the caller's own company - no {id} in the route, the company
// comes from the JWT inside the handlers (CodingRules 7.4). "current" and "options" are
// literal segments, so nothing can bind over them.
[ApiController]
[Route("api/company-information/companies")]
[Authorize]
public class CompanyController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    [HasPermission(PermissionKeys.CompanyGeneral, PermissionAction.View)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct) =>
        Ok(await sender.Send(new GetCompanyGeneralQuery(), ct));

    [HttpGet("options")]
    [HasPermission(PermissionKeys.CompanyGeneral, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetCompanyGeneralOptionsQuery(), ct));

    [HttpPut("current")]
    [HasPermission(PermissionKeys.CompanyGeneral, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateCurrent(
        UpdateCompanyGeneralCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));
}
