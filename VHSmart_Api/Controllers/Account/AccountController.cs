using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Account.Password;
using VHSmart_Api.Features.Account.Profile;
using VHSmart_Api.Features.Account.ProfilePicture;
using VHSmart_Api.Features.Account.Subscription;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Account;

// Account Setting (spec 6.4): a self-service screen every user opens from the top bar, so all
// actions carry the Account.Setting key with View. The seeded roles hold View only
// (A-01); gating change password behind Edit would lock ordinary users out of their own
// password. The FallbackPolicy challenges anonymous callers with 401 before this runs.
[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(ISender sender) : ControllerBase
{
    [HttpGet("profile")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
        => Ok(await sender.Send(new GetProfileQuery(), ct));

    [HttpPut("password")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpPost("profile-picture")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> UploadProfilePicture(
        [FromForm] UploadProfilePictureCommand command,
        CancellationToken ct)
        => Ok(await sender.Send(command, ct));

    [HttpGet("profile-picture")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> GetProfilePicture(CancellationToken ct)
    {
        var response = await sender.Send(new GetProfilePictureQuery(), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    // Subscription tab (spec 6.4): header fields plus the SUBSCRIPTION HISTORY table. These
    // two stay reachable when the subscription has expired (D-11) - the middleware exempts
    // this path so the owner can see the expiry date and renew.
    [HttpGet("subscription")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
        => Ok(await sender.Send(new GetSubscriptionQuery(), ct));

    [HttpGet("subscription/history")]
    [HasPermission(PermissionKeys.AccountSetting, PermissionAction.View)]
    public async Task<IActionResult> GetSubscriptionHistory(
        [FromQuery] GetSubscriptionHistoryQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));
}
