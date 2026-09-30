using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Lookups;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Read-only state lookup of the reference data (R-01). Same permission reasoning as
// CountryController: the rows are global reference data, not a screen of their own.
[ApiController]
[Route("api/admin/states")]
[Authorize]
public class StateController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? countryId, CancellationToken ct)
        => Ok(await sender.Send(new GetStatesQuery(countryId), ct));
}
