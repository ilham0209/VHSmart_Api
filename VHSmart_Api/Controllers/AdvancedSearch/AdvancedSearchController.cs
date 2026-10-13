using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.AdvancedSearch;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.AdvancedSearch;

// Advanced Search (spec 8): currently only the Premise view - the search, the Excel export
// and the filter options, all behind the AdvancedSearch screen key (CodingRules 8.2; the
// legacy export's unauthenticated, view-name-driven download [CODE] is deliberately not
// replicated: this route is permission-gated and hard-wired to the premise view). The other
// eleven views are BLOCKED and land in DS-03. Country / State dropdowns reuse the existing
// GET api/admin/countries + states lookups.
[ApiController]
[Route("api/advanced-search")]
[Authorize]
public class AdvancedSearchController(ISender sender) : ControllerBase
{
    [HttpGet("premises")]
    [HasPermission(PermissionKeys.AdvancedSearch, PermissionAction.View)]
    public async Task<IActionResult> Search(
        [FromQuery] SearchPremisesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Excel export of the same filtered result set (spec 8 buttons: "Search", "Export to
    // Excel", "Reset").
    [HttpGet("premises/export")]
    [HasPermission(PermissionKeys.AdvancedSearch, PermissionAction.View)]
    public async Task<IActionResult> Export(
        [FromQuery] ExportPremisesToExcelQuery query,
        CancellationToken ct)
    {
        var response = await sender.Send(query, ct);
        return File(response.Content, response.ContentType, response.FileName);
    }

    [HttpGet("premise-options")]
    [HasPermission(PermissionKeys.AdvancedSearch, PermissionAction.View)]
    public async Task<IActionResult> GetPremiseOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetPremiseSearchOptionsQuery(), ct));
}
