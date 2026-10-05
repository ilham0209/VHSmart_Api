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

    // Tab "Manage Ingredient Information" (spec 9.1): the table, its "+ Add New Ingredient"
    // source, the link/unlink halves of the Action icon and the certificate download. Every
    // action carries the >= 1 brand rule of spec 6.3 / 9.1 (422) - the handlers enforce it.
    [HttpGet("{id:guid}/ingredients")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetIngredients(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetProductIngredientsQuery(id), ct));

    // Literal segment after {id:guid} so it never binds as an ingredient id.
    [HttpGet("{id:guid}/ingredients/options")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetIngredientOptions(
        Guid id,
        [FromQuery] GetProductIngredientOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query with { ProductId = id }, ct));

    [HttpPost("{id:guid}/ingredients")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Create)]
    public async Task<IActionResult> LinkIngredient(
        Guid id,
        LinkProductIngredientCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { ProductId = id }, ct));

    [HttpDelete("{id:guid}/ingredients/{ingredientId:guid}")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.Delete)]
    public async Task<IActionResult> UnlinkIngredient(
        Guid id,
        Guid ingredientId,
        CancellationToken ct)
    {
        await sender.Send(new UnlinkProductIngredientCommand(id, ingredientId), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/ingredients/{ingredientId:guid}/certificate")]
    [HasPermission(PermissionKeys.ProductManageProduct, PermissionAction.View)]
    public async Task<IActionResult> GetIngredientCertificate(
        Guid id,
        Guid ingredientId,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new GetProductIngredientCertificateQuery(id, ingredientId), ct);
        return File(response.Content, response.ContentType);
    }

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
