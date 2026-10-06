using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Product;

// Manage Menu (spec 9.2): list + form CRUD, the form's dropdown sources, the modal's
// "List of Raw Materials" picker and the toolbar's "Multiple Delete". [HasPermission] gates
// every action on the Product.ManageMenu screen key (CodingRules 8.2); the handlers apply the
// special "Accessible For" visibility rule (CodingRules 7.3) and the ownership check.
[ApiController]
[Route("api/product/manage-menus")]
[Authorize]
public class MenuController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllMenusQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // Literal segments before {id:guid} so they never bind as an id.
    [HttpGet("options")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.View)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        Ok(await sender.Send(new GetMenuOptionsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMenuByIdQuery(id), ct));

    // The modal's "List of Raw Materials*" link button (spec 9.2): the attached table comes
    // back with the menu detail, this is the picker of what may still be attached.
    [HttpGet("{id:guid}/ingredients/options")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.View)]
    public async Task<IActionResult> GetIngredientOptions(
        Guid id,
        [FromQuery] GetMenuIngredientOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query with { MenuId = id }, ct));

    [HttpPost]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateMenuCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateMenuCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteMenuCommand(id), ct);
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [HasPermission(PermissionKeys.ProductManageMenu, PermissionAction.Delete)]
    public async Task<IActionResult> BulkDelete(
        BulkDeleteMenusCommand command,
        CancellationToken ct)
    {
        await sender.Send(command, ct);
        return NoContent();
    }
}
