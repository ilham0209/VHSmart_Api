using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Product.ManageMenuConcept;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Product;

// Manage Menu Concept (spec 9.3): list + form CRUD, the concept's "List of Menu" table and
// the Link button's source. [HasPermission] gates every action on the Product.ManageMenuConcept
// screen key (CodingRules 8.2); the handlers rely on the plain tenant filter - concepts have
// no "Accessible For" table, so only the owner company (or a Switch Company = ALL token) sees
// a row.
[ApiController]
[Route("api/product/manage-menu-concepts")]
[Authorize]
public class MenuConceptController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllMenuConceptsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetMenuConceptByIdQuery(id), ct));

    // The "List of Menu" Link button (spec 9.3): what may still be linked to this concept.
    // Literal segment before {id:guid} so it never binds as an id.
    [HttpGet("{id:guid}/menus/options")]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.View)]
    public async Task<IActionResult> GetMenuOptions(
        Guid id,
        [FromQuery] GetMenuConceptMenuOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query with { MenuConceptId = id }, ct));

    // Spec 9.3: the modal saves the concept first, its "List of Menu" is saved by the PUT.
    [HttpPost]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateMenuConceptCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateMenuConceptCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.ProductManageMenuConcept, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteMenuConceptCommand(id), ct);
        return NoContent();
    }
}
