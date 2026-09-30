using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Manage Companies (spec 6.3). [HasPermission] gates every action on the Admin.Companies
// screen key (CodingRules 8.2); each handler additionally requires IsPlatformAdmin - the
// screen belongs to the Serunai Super User only (D-07). There is no DELETE: the spec 6.3 list
// carries only view and edit actions (legacy delete is [CODE] and was not replicated).
[ApiController]
[Route("api/admin/companies")]
[Authorize]
public class CompanyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminCompanies, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllCompaniesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before {id:guid} so "options" can never bind as a company id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.AdminCompanies, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetCompanyOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminCompanies, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCompanyByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AdminCompanies, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateCompanyCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AdminCompanies, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCompanyCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));
}
