using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

// Fixtures for the F-07 authorization pipeline. They live in the test assembly so the product
// exposes no endpoint this task does not need (AGENTS.md 1).
[ApiController]
[Route("api/test/permissions")]
[Authorize]
public class PermissionTestController : ControllerBase
{
    [HttpGet("view")]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.View)]
    public IActionResult View() => Ok();

    [HttpGet("delete")]
    [HasPermission(PermissionKeys.AuditRecommendation, PermissionAction.Delete)]
    public IActionResult Delete() => Ok();

    // Authenticated but no [HasPermission]: proves the permission check is per action, not a
    // blanket deny for authenticated callers.
    [HttpGet("authenticated-only")]
    public IActionResult AuthenticatedOnly() => Ok();
}

// No authorization metadata at all, so the fallback policy is what answers for it.
[ApiController]
[Route("api/test/open")]
public class OpenTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
