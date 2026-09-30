using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Lookups;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Read-only country lookup of the reference data (R-01). Appendix A has no key for a lookup,
// and the rows are global: gating on a screen key would empty the dropdown on every other
// screen, so the endpoints use Dashboard - the one View all three seeded roles have (D-19).
[ApiController]
[Route("api/admin/countries")]
[Authorize]
public class CountryController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetCountriesQuery(), ct));
}
