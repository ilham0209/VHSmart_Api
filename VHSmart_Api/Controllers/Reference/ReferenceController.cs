using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Reference;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Reference;

// Reference menu (spec 17.1 [CONFIRMED]): the read-only card grid and their icons, behind
// the Reference.View screen key (CodingRules 8.2; the Appendix A key of this screen - only
// the VH Smart Admin role holds it in the D-19 seed). No mutations: the spec calls the page
// a viewer, the writing happens through Admin > Reference Data > Web Links (R-05).
[ApiController]
[Route("api/reference")]
[Authorize]
public class ReferenceController(ISender sender) : ControllerBase
{
    [HttpGet("web-links")]
    [HasPermission(PermissionKeys.ReferenceView, PermissionAction.View)]
    public async Task<IActionResult> GetWebLinks(CancellationToken ct) =>
        Ok(await sender.Send(new GetReferenceWebLinksQuery(), ct));

    [HttpGet("web-links/{id:guid}/icon")]
    [HasPermission(PermissionKeys.ReferenceView, PermissionAction.View)]
    public async Task<IActionResult> GetWebLinkIcon(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetReferenceWebLinkIconQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }
}
