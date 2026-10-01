using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Notifications;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// The bell lives in the shell on every screen, so it carries the Dashboard key - the one
// permission all seeded roles hold (D-19, same call the R-01 lookups made). Appendix A has no
// Notifications key; mark-read also uses View because Dashboard/Edit is not granted to the
// non-admin roles and the row belongs to the caller anyway. Owner may add a dedicated key.
[ApiController]
[Route("api/admin/notifications")]
[Authorize]
public class NotificationController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllNotificationsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    // Literal route before anything that could bind as {id:guid}.
    [HttpGet("unread-count")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
        => Ok(await sender.Send(new GetUnreadCountQuery(), ct));

    [HttpPut("{id:guid}/read")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await sender.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }
}
