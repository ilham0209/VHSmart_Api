using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Roles;

public record RolePermissionInput(
    string PermissionKey,
    bool CanView,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete);

// Assign = replace the whole matrix of one role in one call (Role Configuration, platform admin
// only, spec 3.4). Keys the caller does not send are revoked; every flag false stores no row,
// which is the same as denied (default deny, D-19).
public record UpdateRolePermissionsCommand(Guid RoleId, IReadOnlyList<RolePermissionInput> Permissions)
    : IRequest<RolePermissionsResponse>;

public class UpdateRolePermissionsValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();

        RuleFor(command => command.Permissions).NotNull();

        RuleForEach(command => command.Permissions).ChildRules(permission =>
        {
            permission.RuleFor(input => input.PermissionKey)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(100)
                .Must(key => PermissionKeys.All.Contains(key, StringComparer.Ordinal))
                .WithMessage("Unknown permission key '{PropertyValue}'.");
        });

        RuleFor(command => command.Permissions)
            .Must(permissions => permissions is null || DistinctKeys(permissions))
            .WithMessage("Each permission key may only be assigned once.");
    }

    private static bool DistinctKeys(IReadOnlyList<RolePermissionInput> permissions) =>
        permissions
            .Select(permission => permission.PermissionKey)
            .Distinct(StringComparer.Ordinal)
            .Count() == permissions.Count;
}

public class UpdateRolePermissionsHandler(
    VHSmartDbContext db,
    ICurrentUser user,
    IPermissionService permissionService)
    : IRequestHandler<UpdateRolePermissionsCommand, RolePermissionsResponse>
{
    public async Task<RolePermissionsResponse> Handle(
        UpdateRolePermissionsCommand request,
        CancellationToken ct)
    {
        // Role Configuration is a platform-admin screen (D-07, spec 3.1): a company user must
        // never be able to widen any role, not even one holding Admin.Users.
        if (!user.IsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator can manage roles.");

        var role = await db.Roles.FirstOrDefaultAsync(candidate => candidate.Id == request.RoleId, ct)
            ?? throw new NotFoundException("Role not found.");

        // Keys sent with every flag false are not stored: a row with no action is meaningless
        // and would only make the unique index and the matrix harder to read.
        var desired = request.Permissions
            .Where(permission => permission.CanView || permission.CanCreate || permission.CanEdit || permission.CanDelete)
            .ToDictionary(permission => permission.PermissionKey, StringComparer.Ordinal);

        var stored = await db.RolePermissions
            .Where(row => row.RoleId == role.Id)
            .ToListAsync(ct);

        foreach (var row in stored)
        {
            if (desired.Remove(row.PermissionKey, out var input))
            {
                row.CanView = input.CanView;
                row.CanCreate = input.CanCreate;
                row.CanEdit = input.CanEdit;
                row.CanDelete = input.CanDelete;
            }
            else
            {
                // Revoked: soft delete, never a physical DELETE (CodingRules 7.1).
                db.RolePermissions.Remove(row);
            }
        }

        foreach (var (key, input) in desired)
        {
            db.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionKey = key,
                CanView = input.CanView,
                CanCreate = input.CanCreate,
                CanEdit = input.CanEdit,
                CanDelete = input.CanDelete
            });
        }

        await db.SaveChangesAsync(ct);

        // The matrix is cached per role, so the change must be visible to the next request.
        permissionService.Invalidate(role.Id);

        var rows = await db.RolePermissions
            .AsNoTracking()
            .Where(row => row.RoleId == role.Id)
            .ToListAsync(ct);

        return new RolePermissionsResponse(role.Id, role.Name, RolePermissionMatrix.Build(rows));
    }
}
