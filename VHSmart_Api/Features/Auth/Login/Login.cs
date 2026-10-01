using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Auth.Login;

// Login (spec 6.2, D-29): verify the password against AdmUsers, apply the lockout counters
// of the same row, then issue a JWT. The request carries credentials only - every other
// identity value comes from the database and travels in the token, never in the body.
public record LoginCommand(string Email, string Password) : IRequest<SessionResponse>;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty();
    }
}

// The spec gives no login messages (spec 6.2 shows the screen only), so these are ours.
public static class LoginMessages
{
    // One message for "unknown e-mail" and "wrong password": the endpoint must not be usable
    // to enumerate accounts (legacy defect list, spec 23).
    public const string InvalidCredentials = "Invalid email or password.";

    public const string Locked = "Account is locked. Please try again later.";

    public const string Inactive = "Account is inactive. Please contact the administrator.";
}

public class LoginHandler(
    VHSmartDbContext db,
    IJwtTokenService tokenService,
    IConfiguration configuration)
    : IRequestHandler<LoginCommand, SessionResponse>
{
    // Q12: the lockout numbers are not in the spec; config (Login:*) wins when set.
    private const int DefaultMaxFailedAttempts = 5;

    private const int DefaultLockoutMinutes = 15;

    public async Task<SessionResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLower();

        // No IgnoreQueryFilters: a soft-deleted user must not be able to sign in (and the
        // uniqueness rule of spec 21.9 is also "among non-deleted users").
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == email, ct);
        if (user is null)
            throw new UnauthorizedException(LoginMessages.InvalidCredentials);

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
            throw new UnauthorizedException(LoginMessages.Locked);

        // An account that has not been activated yet has no password at all (C-02: the
        // default password exists only after activation). Refuse before the hasher sees an
        // empty string - same generic answer as a wrong password, no enumeration.
        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedException(LoginMessages.InvalidCredentials);

        var verification = UserPasswordHasher.Verify(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RegisterFailedAttemptAsync(user, ct);
            throw new UnauthorizedException(LoginMessages.InvalidCredentials);
        }

        if (!user.IsActive)
            throw new ForbiddenException(LoginMessages.Inactive);

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = UserPasswordHasher.Hash(user, request.Password);
        await db.SaveChangesAsync(ct);

        return await SessionBuilder.BuildAsync(db, tokenService, user, ct);
    }

    private async Task RegisterFailedAttemptAsync(UserEntity user, CancellationToken ct)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= MaxFailedAttempts())
        {
            // The lock itself is the penalty, so the counter restarts; after the lock
            // expires the user gets a full set of attempts again.
            user.FailedLoginCount = 0;
            user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes());
        }

        await db.SaveChangesAsync(ct);
    }

    private int MaxFailedAttempts() =>
        int.TryParse(configuration["Login:MaxFailedAttempts"], out var attempts) && attempts > 0
            ? attempts
            : DefaultMaxFailedAttempts;

    private int LockoutMinutes() =>
        int.TryParse(configuration["Login:LockoutMinutes"], out var minutes) && minutes > 0
            ? minutes
            : DefaultLockoutMinutes;
}
