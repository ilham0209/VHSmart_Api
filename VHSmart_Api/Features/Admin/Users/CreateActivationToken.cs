using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// Re-issue the activation token (spec 21.8 "resend activation"): the account was never
// activated and the first token was lost or has expired, so the admin mints a fresh one and
// the old unused tokens are soft-deleted - only the newest link may ever work. The raw token
// is returned exactly like on add; nothing is e-mailed (no password e-mail, spec 23).
public record CreateActivationTokenCommand(Guid Id) : IRequest<CreateActivationTokenResponse>;

public class CreateActivationTokenValidator : AbstractValidator<CreateActivationTokenCommand>
{
    public CreateActivationTokenValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public record CreateActivationTokenResponse(Guid UserId, string ActivationToken);

public class CreateActivationTokenHandler(
    VHSmartDbContext db,
    ICurrentUser caller,
    IConfiguration configuration)
    : IRequestHandler<CreateActivationTokenCommand, CreateActivationTokenResponse>
{
    public async Task<CreateActivationTokenResponse> Handle(
        CreateActivationTokenCommand request,
        CancellationToken ct)
    {
        var user = await UserLookup.GetVisibleUserAsync(db, caller, request.Id, ct);

        if (user.ActivatedAt is not null)
            throw new BusinessRuleException("This account is already activated.");

        var stale = await db.UserTokens
            .Where(token =>
                token.UserId == user.Id
                && token.Purpose == UserTokenPurpose.Activation
                && token.UsedAt == null)
            .ToListAsync(ct);
        db.UserTokens.RemoveRange(stale);

        var (rawToken, tokenHash, expiresAt) = OneTimeToken.Issue(configuration);
        db.UserTokens.Add(new UserTokenEntity
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.Activation,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(ct);
        return new CreateActivationTokenResponse(user.Id, rawToken);
    }
}
