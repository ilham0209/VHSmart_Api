using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// Add a user (spec 3.2, legacy rules of spec 3.4): at least one company ("Please add List of
// Company"), name stored in upper case, e-mail unique among non-deleted users (spec 21.9),
// company and creator taken from the session, never from the client (CodingRules 8.1).
// The account starts inactive with no password: an activation token is issued instead and
// returned once, replacing the legacy activation e-mail that carried a password (spec 23).
public record CreateUserCommand(
    string Name,
    string Email,
    Guid RoleId,
    string? ContactNo,
    IReadOnlyList<Guid> CompanyIds) : IRequest<CreateUserResponse>;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
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

public record CreateUserResponse(
    Guid Id,
    string Name,
    string Email,
    string ActivationToken);

public class CreateUserHandler(
    VHSmartDbContext db,
    ICurrentUser caller,
    IConfiguration configuration)
    : IRequestHandler<CreateUserCommand, CreateUserResponse>
{
    public async Task<CreateUserResponse> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // Spec 21.9: unique across non-deleted users; the global filter already excludes
        // deleted rows, so their addresses are free again.
        var duplicate = await db.Users.AsNoTracking()
            .AnyAsync(user => user.Email == email, ct);
        if (duplicate)
            throw new ConflictException("A user with this e-mail already exists.");

        var companyIds = await UserScope.ResolveCompanyIdsAsync(db, caller, request.CompanyIds, ct);
        // The validator already refuses an empty list with the spec message; this guard keeps
        // the rule true for direct handler calls too (>= 1 company, spec 3.4).
        if (companyIds.Count == 0)
            throw new BusinessRuleException("Please add List of Company");
        _ = await UserScope.ResolveRoleAsync(db, caller, request.RoleId, ct);

        var user = new UserEntity
        {
            Name = name,
            Email = email,
            // No password exists until activation (spec 3.2: the default password is
            // generated when the account is activated); login refuses an empty hash.
            PasswordHash = string.Empty,
            RoleId = request.RoleId,
            ContactNo = string.IsNullOrWhiteSpace(request.ContactNo) ? null : request.ContactNo.Trim(),
            IsActive = false,
            IsPlatformAdmin = false,
            MustChangePassword = false,
            ActivatedAt = null
        };
        db.Users.Add(user);

        // "Please add List of Company" (spec 3.4) guarantees >= 1; the first requested
        // company becomes the default membership login picks (spec 6.2).
        var first = true;
        foreach (var companyId in companyIds)
        {
            db.UserCompanies.Add(new UserCompanyEntity
            {
                UserId = user.Id,
                CompanyId = companyId,
                IsDefault = first
            });
            first = false;
        }

        var (rawToken, tokenHash, expiresAt) = OneTimeToken.Issue(configuration);
        db.UserTokens.Add(new UserTokenEntity
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.Activation,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(ct);

        return new CreateUserResponse(user.Id, user.Name, user.Email, rawToken);
    }
}
