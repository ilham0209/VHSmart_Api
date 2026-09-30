using Microsoft.AspNetCore.Authorization;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Grants the requirement only when the caller's role holds that key + action (CodingRules 8.2).
// The role comes from ICurrentUser, i.e. from the JWT - never from the body or the URL (8.1).
// Anything not explicitly granted stays denied: no role claim, unknown key, or no row.
public sealed class PermissionAuthorizationHandler(
    IPermissionService permissionService,
    ICurrentUser currentUser) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        if (currentUser.RoleId == Guid.Empty)
            return;

        // The authorization middleware passes the current HttpContext as the resource; without
        // one (unit tests, background calls) fall back to an uncancelable token.
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;

        if (await permissionService.HasPermissionAsync(
                currentUser.RoleId,
                requirement.Key,
                requirement.Action,
                cancellationToken))
            context.Succeed(requirement);
    }
}
