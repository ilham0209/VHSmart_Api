using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Product.ManageProduct;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Product;

// Manage Product (spec 9.1): list + form CRUD, the form's dropdown sources and the toolbar's
// "Multiple Delete". [HasPermission] gates every action on the Product.ManageProduct screen
// key (CodingRules 8.2); the handlers apply the ownership check (CompanyId from the JWT).
[ApiController]
[Route("api/product/manage-products")]
[Authorize]
public class ProductController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllProductsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segments before {id:guid} so they never bind as an id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetProductOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetProductByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateProductCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProductCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Delete)]
    public async Task<IActionResult> BulkDelete(
        BulkDeleteProductsCommand command,
        CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
