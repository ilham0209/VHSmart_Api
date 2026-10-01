using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// Edit a user (spec 3.2). Same field rules as add; the e-mail check excludes the row itself
// (spec 21.9 checks duplicates for new users, an unchanged address must not block the save).
// The Status column (Active / Inactive) is editable here; the password, activation state and
// the platform-admin flag are not form fields and are never touched by this command.
// Company list: requested companies must be assignable (UserScope, 404 otherwise); a company
// outside the caller's scope is preserved untouched, so a company admin editing a
// multi-company user can never strip links they are not allowed to manage.
public record UpdateUserCommand(
    Guid Id,
    string Name,
    string Email,
    Guid RoleId,
    string? ContactNo,
    bool IsActive,
    IReadOnlyList<Guid> CompanyIds) : IRequest<UserDetailsResponse>;

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must be 200 characters or fewer.");
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(254);
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role is required.");
        RuleFor(x => x.ContactNo).MaximumLength(30);
        RuleFor(x => x.CompanyIds)
            .NotEmpty().WithMessage("Please add List of Company");
    }
}

public class UpdateUserHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<UpdateUserCommand, UserDetailsResponse>
{
    public async Task<UserDetailsResponse> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await UserLookup.GetVisibleUserAsync(db, caller, request.Id, ct);

        var name = request.Name.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        var duplicate = await db.Users.AsNoTracking()
            .AnyAsync(row => row.Email == email && row.Id != request.Id, ct);
        if (duplicate)
            throw new ConflictException("A user with this e-mail already exists.");

        var companyIds = await UserScope.ResolveCompanyIdsAsync(db, caller, request.CompanyIds, ct);
        // The validator already refuses an empty list with the spec message; this guard keeps
        // the rule true for direct handler calls too (>= 1 company, spec 3.4).
        if (companyIds.Count == 0)
            throw new BusinessRuleException("Please add List of Company");
        _ = await UserScope.ResolveRoleAsync(db, caller, request.RoleId, ct);

        var tracked = await db.Users.SingleAsync(row => row.Id == request.Id, ct);
        tracked.Name = name;
        tracked.Email = email;
        tracked.RoleId = request.RoleId;
        tracked.ContactNo = string.IsNullOrWhiteSpace(request.ContactNo) ? null : request.ContactNo.Trim();
        tracked.IsActive = request.IsActive;

        await ApplyMembershipsAsync(tracked, companyIds, ct);
        await db.SaveChangesAsync(ct);

        return await UserDetailsResponse.FromAsync(db, tracked, ct);
    }

    private async Task ApplyMembershipsAsync(
        UserEntity user,
        IReadOnlyList<Guid> requested,
        CancellationToken ct)
    {
        var existing = await db.UserCompanies
            .Where(membership => membership.UserId == user.Id)
            .ToListAsync(ct);

        // Outside the caller's scope the link is invisible and therefore untouchable - keep
        // it; inside the scope the request is a full replace.
        var assignable = caller.IsPlatformAdmin
            ? null
            : (Guid?)caller.CompanyId;
        var manageable = existing
            .Where(membership => assignable is null || membership.CompanyId == assignable)
            .ToList();

        foreach (var membership in manageable.Where(row => !requested.Contains(row.CompanyId)))
            db.UserCompanies.Remove(membership);

        var kept = manageable.Where(row => requested.Contains(row.CompanyId)).ToList();
        var present = kept.Select(row => row.CompanyId).ToHashSet();
        var added = new List<UserCompanyEntity>();
        foreach (var companyId in requested.Where(id => !present.Contains(id)))
        {
            var membership = new UserCompanyEntity
            {
                UserId = user.Id,
                CompanyId = companyId,
                IsDefault = false
            };
            db.UserCompanies.Add(membership);
            added.Add(membership);
        }

        // Exactly one default must survive (login picks it, spec 6.2). Keep the current one
        // whenever its link stays - including a link outside the caller's scope, which this
        // command may not touch - and only promote the first company of the request when the
        // default link was just removed.
        var survivingDefault = existing
            .Where(row => !manageable.Contains(row) || requested.Contains(row.CompanyId))
            .Any(row => row.IsDefault);
        if (!survivingDefault)
        {
            var target = kept.FirstOrDefault(row => row.CompanyId == requested[0])
                ?? added.FirstOrDefault(row => row.CompanyId == requested[0]);
            if (target is not null)
                target.IsDefault = true;
        }
    }
}
