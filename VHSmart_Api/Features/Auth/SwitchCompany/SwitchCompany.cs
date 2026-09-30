using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Auth.SwitchCompany;

// Switch Company (spec 6.2/7.4, D-29): re-issue the token for another company the caller
// already belongs to. The target id arrives in the body (it is a choice, not an identity)
// and is checked against AdmUserCompanies; nothing is written - the active company lives
// only in the token.
public record SwitchCompanyCommand(Guid CompanyId) : IRequest<SessionResponse>;

public class SwitchCompanyValidator : AbstractValidator<SwitchCompanyCommand>
{
    public SwitchCompanyValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
    }
}

public class SwitchCompanyHandler(
    VHSmartDbContext db,
    ICurrentUser currentUser,
    IJwtTokenService tokenService)
    : IRequestHandler<SwitchCompanyCommand, SessionResponse>
{
    public async Task<SessionResponse> Handle(SwitchCompanyCommand request, CancellationToken ct)
    {
        // The user id comes from the token only (CodingRules 7.4), never from the body.
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            throw new UnauthorizedException(LoginMessages.InvalidCredentials);

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new UnauthorizedException(LoginMessages.InvalidCredentials);

        if (!user.IsActive)
            throw new ForbiddenException(LoginMessages.Inactive);

        // A company the caller does not belong to answers 404, never 403 (CodingRules 9):
        // do not reveal which company ids exist.
        var isMember = await db.UserCompanies.AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId && membership.CompanyId == request.CompanyId, ct);
        if (!isMember)
            throw new NotFoundException("Company not found.");

        return await SessionBuilder.BuildAsync(db, tokenService, user, request.CompanyId, ct);
    }
}
