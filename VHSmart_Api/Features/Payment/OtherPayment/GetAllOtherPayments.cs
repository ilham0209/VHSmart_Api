using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Payment.OtherPayment;

// "List of Other Payments" (spec 15.2 [CONFIRMED] columns): Action is client side (only the
// Id is needed). Payment Category is the name of the linked General Data row (Group PAYMENT /
// "Payment Category") and Payee / Payee Bank Name / Payee Bank Account No. are read live from
// the linked Service Provider - Database.md 12 stores only the two ids. Amount, Payment
// Voucher No. and Payment Status are the payment's own columns; Created is SysDateCreated.
// The View filter (default "Default") carries no documented values - it is accepted and
// "Default"/empty means no filter, any other value filters Payment Status verbatim (the same
// flag the Certificate list carries). Payment Category (default "All") filters the linked
// General Data row by id - the dropdown is populated from the category lookup, "All"/empty
// meaning no filter (flagged: the spec shows a dropdown but never its payload). The spec
// states the default order: Created newest first. Search covers the visible text columns.
public record GetAllOtherPaymentsQuery
    : IRequest<DataGridResponse<GetAllOtherPaymentsResponse>>
{
    public DataGridRequest Request { get; set; } = new();

    public string? View { get; set; }

    public Guid? PaymentCategoryId { get; set; }
}

public record GetAllOtherPaymentsResponse(
    Guid Id,
    string PaymentCategory,
    string Payee,
    string? PayeeBankName,
    string? PayeeBankAccountNo,
    decimal Amount,
    string? PaymentVoucherNo,
    string PaymentStatus,
    DateTime CreatedDate);

public class GetAllOtherPaymentsHandler(VHSmartDbContext db, ICurrentUser user)
    : IRequestHandler<GetAllOtherPaymentsQuery, DataGridResponse<GetAllOtherPaymentsResponse>>
{
    public async Task<DataGridResponse<GetAllOtherPaymentsResponse>> Handle(
        GetAllOtherPaymentsQuery request,
        CancellationToken ct)
    {
        var useDefaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = useDefaultSort
            ? nameof(GetAllOtherPaymentsResponse.CreatedDate)
            : request.Request.SortBy;
        var sortDescending = useDefaultSort || request.Request.SortDescending;

        var viewFilter = string.IsNullOrWhiteSpace(request.View)
            || string.Equals(request.View, "Default", StringComparison.OrdinalIgnoreCase)
            ? null
            : request.View;

        var paymentCategoryIdFilter = request.PaymentCategoryId is null
            || request.PaymentCategoryId == Guid.Empty
            ? null
            : request.PaymentCategoryId;

        return await db.Payments.OfType<OtherPaymentEntity>()
            .AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId
                && (viewFilter == null || row.Status == viewFilter)
                && (paymentCategoryIdFilter == null
                    || row.PaymentCategoryId == paymentCategoryIdFilter))
            .Select(row => new GetAllOtherPaymentsResponse(
                row.Id,
                db.GeneralData
                    .Where(data => data.Id == row.PaymentCategoryId)
                    .Select(data => data.Name)
                    .FirstOrDefault() ?? string.Empty,
                db.ServiceProviders
                    .Where(provider => provider.Id == row.ServiceProviderId)
                    .Select(provider => provider.Name)
                    .FirstOrDefault() ?? string.Empty,
                db.ServiceProviders
                    .Where(provider => provider.Id == row.ServiceProviderId)
                    .Select(provider => provider.BankName)
                    .FirstOrDefault(),
                db.ServiceProviders
                    .Where(provider => provider.Id == row.ServiceProviderId)
                    .Select(provider => provider.BankAccountNo)
                    .FirstOrDefault(),
                row.Amount,
                row.PaymentVoucherNo,
                row.Status,
                row.SysDateCreated))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetAllOtherPaymentsResponse.PaymentCategory),
                nameof(GetAllOtherPaymentsResponse.Payee),
                nameof(GetAllOtherPaymentsResponse.PayeeBankName),
                nameof(GetAllOtherPaymentsResponse.PayeeBankAccountNo),
                nameof(GetAllOtherPaymentsResponse.PaymentVoucherNo),
                nameof(GetAllOtherPaymentsResponse.PaymentStatus))
            .ApplySort(sortBy, sortDescending)
            .ToDataGridResponseAsync(request.Request, ct);
    }
}
