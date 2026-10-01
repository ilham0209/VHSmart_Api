using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Account.Subscription;

// SUBSCRIPTION HISTORY table (spec 6.4): one row per subscription period, labelled like
// "RENEWAL : VH SMART - Advanced (1 Year)". The No. column numbers the rows across the whole
// list (1-based, continuing onto the next page), the Search box covers the label and the
// package name, and the default order is the newest period first - same default as the bell.
public record GetSubscriptionHistoryQuery : IRequest<DataGridResponse<GetSubscriptionHistoryResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetSubscriptionHistoryResponse(
    Guid Id,
    int No,
    string? Label,
    string PackageName,
    int DurationMonths,
    DateTime StartDate,
    DateTime EndDate);

public class GetSubscriptionHistoryHandler(VHSmartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSubscriptionHistoryQuery, DataGridResponse<GetSubscriptionHistoryResponse>>
{
    public async Task<DataGridResponse<GetSubscriptionHistoryResponse>> Handle(
        GetSubscriptionHistoryQuery request,
        CancellationToken ct)
    {
        // Same identity preamble as the rest of Account Setting: soft-deleted 404, inactive 403.
        await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        var defaultSort = string.IsNullOrWhiteSpace(request.Request.SortBy);
        var sortBy = defaultSort ? nameof(GetSubscriptionHistoryResponse.StartDate) : request.Request.SortBy;
        var descending = defaultSort || request.Request.SortDescending;

        // Left join: a package the owner soft-deleted later must not hide its history rows
        // (the name simply comes back empty). The CompanyId match keeps a ViewAllCompanies
        // caller on their own rows - the tenant filter alone is wide open for them.
        var response = await (
                from subscription in db.CompanySubscriptions.AsNoTracking()
                    .Where(x => x.CompanyId == currentUser.CompanyId)
                join package in db.SubscriptionPackages.AsNoTracking()
                    on subscription.PackageId equals package.Id into packages
                from package in packages.DefaultIfEmpty()
                select new GetSubscriptionHistoryResponse(
                    subscription.Id,
                    0,
                    subscription.Label,
                    package == null ? string.Empty : package.Name,
                    subscription.DurationMonths,
                    subscription.StartDate,
                    subscription.EndDate))
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(GetSubscriptionHistoryResponse.Label),
                nameof(GetSubscriptionHistoryResponse.PackageName))
            .ApplySort(sortBy, descending)
            .ToDataGridResponseAsync(request.Request, ct);

        // "No." is position in the whole list, not on the page: 1..PageSize on page 1 and
        // continuing from (page - 1) * pageSize afterwards.
        var offset = (response.CurrentPage - 1) * response.PageSize;
        response.Data = response.Data
            .Select((row, index) => row with { No = offset + index + 1 })
            .ToList();

        return response;
    }
}
