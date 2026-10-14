using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Support;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Support;

// Support screen (spec 17.2): the feedback card behind Support.Submit (Appendix A key,
// Create action - the form inserts one row; the house PermissionAction has no Submit).
// POST only - the spec is a submit form with no list, and the delivery side is HOLD -
// owner (spec 17.2).
[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController(ISender sender) : ControllerBase
{
    [HttpPost("feedback")]
    [HasPermission(PermissionKeys.SupportSubmit, PermissionAction.Create)]
    public async Task<IActionResult> SubmitFeedback(
        [FromForm] SubmitFeedbackCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));
}
