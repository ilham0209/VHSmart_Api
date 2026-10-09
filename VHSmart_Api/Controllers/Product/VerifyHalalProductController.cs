using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Product.VerifyHalalProductUpdate;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Product;

// Verify Halal Product Update (spec 9.4): the local list of the company's products with
// their Verify Halal publish status, the "View By" category dropdown source and the publish
// status toggle. D-28: no external Verify Halal call. [HasPermission] gates every action on
// the Product.VerifyHalalProductUpdate screen key (CodingRules 8.2); the handlers apply the
// ownership check (CompanyId from the JWT).
[ApiController]
[Route("api/product/verify-halal-products")]
[Authorize]
public class VerifyHalalProductController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.ProductVerifyHalalProductUpdate, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetVerifyHalalProductsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segment before {id:guid} so it never binds as a product id.
    [HttpGet("category-options")]
    [HasPermission(PermissionKeys.ProductVerifyHalalProductUpdate, PermissionAction.View)]
    public async Task<IActionResult> GetCategoryOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetVerifyHalalCategoryOptionsQuery(), ct));

    [HttpPut("{id:guid}/publish-status")]
    [HasPermission(PermissionKeys.ProductVerifyHalalProductUpdate, PermissionAction.Edit)]
    public async Task<IActionResult> UpdatePublishStatus(
        Guid id,
        UpdateVerifyHalalPublishStatusCommand command,
        CancellationToken ct)
    {
        await sender.Send(command with { Id = id }, ct);
        return NoContent();
    }
}
