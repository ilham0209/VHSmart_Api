using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Extensions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Features.Admin.Users;

// Manage Users list (spec 3.2, CONFIRMED columns): Name, Email, Role, Company as a list,
// Created Date and Status (Active / Inactive). The Action column is client-side and only
// needs the Id. The single "System User" view of the spec has no second option, so there is
// no view filter. Scope (spec 3.3, D-07): the platform admin sees every user, a company
// admin only users linked to the active company. Search runs in SQL on Name/Email; the role
// and company names are display joins loaded after, the way the C-01 screen loads brands.
public record GetAllUsersQuery : IRequest<DataGridResponse<GetAllUsersResponse>>
{
    public DataGridRequest Request { get; set; } = new();
}

public record GetAllUsersResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    IReadOnlyList<string> Companies,
    DateTime CreatedDate,
    string Status);

public class GetAllUsersHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<GetAllUsersQuery, DataGridResponse<GetAllUsersResponse>>
{
    public async Task<DataGridResponse<GetAllUsersResponse>> Handle(
        GetAllUsersQuery request,
        CancellationToken ct)
    {
        var query = db.Users
            .AsNoTracking()
            .ApplySearch(
                request.Request.SearchTerm,
                nameof(UserEntity.Name),
                nameof(UserEntity.Email));

        if (!caller.IsPlatformAdmin)
        {
            // AdmUsers is a global table (Database.md 5), so the tenant filter cannot scope
            // it; the membership list does, exactly like the join the spec shows.
            var scopedUserIds = await db.UserCompanies.AsNoTracking()
                .Where(membership => membership.CompanyId == caller.CompanyId)
                .Select(membership => membership.UserId)
                .Distinct()
                .ToListAsync(ct);
            query = query.Where(user => scopedUserIds.Contains(user.Id));
        }

        var users = await query.ToListAsync(ct);
        var rows = await ProjectAsync(users, ct);

        // Sorted and paged in memory: the Companies cell is an aggregated list, which the
        // shared ToDataGridResponseAsync cannot count or slice through EF. The clamping
        // mirrors that helper exactly, so a client still cannot pull the whole table.
        var sorted = rows
            .AsQueryable()
            .ApplySort(request.Request.SortBy, request.Request.SortDescending)
            .ToList();

        var pageSize = request.Request.PageSize <= 0
            ? DataGridRequest.DefaultPageSize
            : Math.Min(request.Request.PageSize, DataGridRequest.MaxPageSize);
        var page = request.Request.Page <= 0 ? 1 : request.Request.Page;
        var totalRecords = sorted.Count;
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        var offset = (int)Math.Min((page - 1) * (long)pageSize, int.MaxValue);

        return new DataGridResponse<GetAllUsersResponse>
        {
            Data = sorted.Skip(offset).Take(pageSize),
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            CurrentPage = page,
            PageSize = pageSize,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    private async Task<List<GetAllUsersResponse>> ProjectAsync(
        IReadOnlyList<UserEntity> users,
        CancellationToken ct)
    {
        if (users.Count == 0)
            return [];

        var userIds = users.Select(user => user.Id).ToList();
        var roleIds = users.Select(user => user.RoleId).Distinct().ToList();

        var roles = await db.Roles.AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .ToDictionaryAsync(role => role.Id, role => role.Name, ct);

        var memberships = await db.UserCompanies.AsNoTracking()
            .Where(membership => userIds.Contains(membership.UserId))
            .ToListAsync(ct);

        var companyIds = memberships.Select(membership => membership.CompanyId).Distinct().ToList();
        var companies = await db.Companies.AsNoTracking()
            .Where(company => companyIds.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, company => company.Name, ct);

        return users.Select(user => new GetAllUsersResponse(
                user.Id,
                user.Name,
                user.Email,
                roles.TryGetValue(user.RoleId, out var roleName) ? roleName : string.Empty,
                memberships
                    .Where(membership => membership.UserId == user.Id)
                    .Select(membership => companies.TryGetValue(membership.CompanyId, out var name)
                        ? name
                        : string.Empty)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                user.SysDateCreated,
                user.IsActive ? "Active" : "Inactive"))
            .ToList();
    }
}
