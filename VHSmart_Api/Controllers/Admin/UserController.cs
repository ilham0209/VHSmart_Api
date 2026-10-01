using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Users;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Manage Users (spec 3.2). [HasPermission] gates every action on the Admin.Users screen key
// (Appendix A, A-01); unlike Manage Companies this screen is not platform-admin only - D-07
// gives the per-company VH Smart Admin the users of their own company, which the handlers
// enforce through UserLookup / UserScope (cross-tenant ids answer 404, never 403).
[ApiController]
[Route("api/admin/users")]
[Authorize]
public class UserController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.View)]
    public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    // Literal routes before {id:guid} so "options" can never bind as a user id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct)
        => Ok(await sender.Send(new GetUserOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetUserByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    // Re-issue the activation token (spec 21.8 "resend activation").
    [HttpPost("{id:guid}/activation-token")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.Edit)]
    public async Task<IActionResult> CreateActivationToken(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new CreateActivationTokenCommand(id), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.AdminUsers, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteUserCommand(id), ct);
        return NoContent();
    }
}
