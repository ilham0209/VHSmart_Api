using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Dashboard;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Dashboard;

// The Dashboard shell (spec 13). Every screen behind its links carries its own permission,
// so the cards only ever show the Dashboard-permitted caller's own data.
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => Ok(await sender.Send(new GetDashboardSummaryQuery(), ct));

    [HttpGet("recent-raw-materials")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetRecentlyAddedRawMaterials(
        [FromQuery] GetRecentlyAddedRawMaterialsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("expiring-typhoid")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetExpiringTyphoidInjection(CancellationToken ct)
        => Ok(await sender.Send(new GetExpiringTyphoidInjectionQuery(), ct));

    [HttpGet("raw-material-by-country")]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetRawMaterialByCountry(CancellationToken ct)
        => Ok(await sender.Send(new GetRawMaterialByCountryQuery(), ct));
}
