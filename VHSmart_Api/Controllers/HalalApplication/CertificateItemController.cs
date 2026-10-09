using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.HalalApplication;

// Halal Application > Halal Certificate > Certificate Item (spec 12.8): the "List of
// Certificate Item" grid and its "CLICK TO ADD" flow (fill the number, Add - the popup
// confirmation is client side). [HasPermission] gates every action on the
// HalalApplication.CertificateItem screen key (CodingRules 8.2) - View for the list, Edit
// for the number entry (it edits one item); the handlers apply the ownership check
// (CompanyId from the JWT).
[ApiController]
[Route("api/halal-application/certificate-items")]
[Authorize]
public class CertificateItemController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.HalalApplicationCertificateItem, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetCertificateItemsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    // "CLICK TO ADD" -> Add: links the item to a live certificate row of the company,
    // creating that row when the number is new (Database.md 10).
    [HttpPut("{id:guid}/certificate-number")]
    [HasPermission(PermissionKeys.HalalApplicationCertificateItem, PermissionAction.Edit)]
    public async Task<IActionResult> AddCertificateNumber(
        Guid id,
        AddCertificateNumberToItemCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { ItemId = id }, ct));
}
