using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.CompanyInformation;

// Company > Halal Policy (spec 7.2): list, add (multipart), download and delete, all on the
// Company.HalalPolicy screen key (CodingRules 8.2). The handlers scope every row to the
// caller's own company - no {id} carries a tenant.
[ApiController]
[Route("api/company-information/halal-policies")]
[Authorize]
public class HalalPolicyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.CompanyHalalPolicy, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllHalalPoliciesQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal route before "{id:guid}" - also unreachable as an id thanks to the guid constraint.
    [HttpGet("{id:guid}/document")]
    [HasPermission(PermissionKeys.CompanyHalalPolicy, PermissionAction.View)]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct)
    {
        var response = await sender.Send(new GetHalalPolicyDocumentQuery(id), ct);
        return new FileStreamResult(response.Content, response.ContentType);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionKeys.CompanyHalalPolicy, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        [FromForm] CreateHalalPolicyCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.CompanyHalalPolicy, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteHalalPolicyCommand(id), ct);
        return NoContent();
    }
}
