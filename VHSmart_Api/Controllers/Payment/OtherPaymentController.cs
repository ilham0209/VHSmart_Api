using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VHSmart_Api.Features.Payment.OtherPayment;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Controllers.Payment;

// Payment > Other (spec 15.2): the list with its View / Payment Category filters, the
// form's payee / payment-category / staff lookups, the detail, the status-workflow save and
// soft delete. [HasPermission] gates every action on the Payment.Other screen key (CodingRules
// 8.2); the handlers apply the tenant scope. The literal lookup segments sit above
// {id:guid} so they never bind as an id. The status workflow itself lives in
// PaymentStatusPolicy (D-23 - the Other chain branches to PAYMENT TO SERVICE PROVIDER), not
// here.
[ApiController]
[Route("api/payment/other")]
[Authorize]
public class OtherPaymentController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.View)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAllOtherPaymentsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("payee-lookup")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.View)]
    public async Task<IActionResult> GetPayeeLookup(
        [FromQuery] GetOtherPaymentPayeeOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("payment-category-lookup")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.View)]
    public async Task<IActionResult> GetPaymentCategoryLookup(
        [FromQuery] GetOtherPaymentCategoryOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("staff-lookup")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.View)]
    public async Task<IActionResult> GetStaffLookup(
        [FromQuery] GetOtherPaymentStaffOptionsQuery query,
        CancellationToken ct) =>
        Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetOtherPaymentByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.Create)]
    public async Task<IActionResult> Create(
        CreateOtherPaymentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    // D-23: the save may also advance the status forward exactly one step.
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateOtherPaymentCommand command,
        CancellationToken ct) =>
        Ok(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionKeys.PaymentOther, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteOtherPaymentCommand(id), ct);
        return NoContent();
    }
}
