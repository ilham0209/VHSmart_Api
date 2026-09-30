using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Admin.Lookups;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Admin;

// Read-only scheme lookup of the reference data (R-01, spec 12.1). Same permission reasoning
// as CountryController: one dropdown serves the picker, Halal Policy, Product and Batch.
[ApiController]
[Route("api/admin/schemes")]
[Authorize]
public class SchemeController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.Dashboard, PermissionAction.View)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetSchemesQuery(), ct));
}
