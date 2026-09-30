using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Service Provider CRUD (spec 5.3). [HasPermission] gates every action on the
// Admin.ServiceProviders screen key (CodingRules 8.2); the handlers apply the tenant scope
// (spec 3.3) and the duplicate rule (spec 21.9). Not platform-admin only: this is per-company
// reference data, unlike Certification Bodies (D-07).
[ApiController]
[Route("api/admin/service-providers")]
[Authorize]
public class ServiceProviderController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminServiceProviders, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllServiceProvidersQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminServiceProviders, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetServiceProviderByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AdminServiceProviders, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateServiceProviderCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AdminServiceProviders, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateServiceProviderCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminServiceProviders, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteServiceProviderCommand(id), ct);
        return NoContent();
    }
}
