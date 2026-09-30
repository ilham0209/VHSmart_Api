using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Roles;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Roles + permissions (A-01). The screens behind it are platform admin only (D-07, spec 3.1);
// [HasPermission] is what stops an authenticated stranger, the handlers stop a non-platform user.
[ApiController]
[Route("api/admin/roles")]
[Authorize]
public class RoleController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.View)]
    public async Task<IActionResult> GetAll([FromQuery] GetAllRoleQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}/permissions")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.View)]
    public async Task<IActionResult> GetPermissions(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetRolePermissionsQuery(id), ct));

    [HttpPut("{id:guid}/permissions")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.Edit)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        UpdateRolePermissionsCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { RoleId = id }, ct));
}
