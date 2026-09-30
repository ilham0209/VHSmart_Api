using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Auth.Login;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Account;

// Identity preamble shared by the Account Setting handlers: the caller is the subject (the id
// comes from the JWT only, CodingRules 7.4), a soft-deleted user answers 404 and a deactivated
// account is blocked everywhere here, same as Switch Company (spec 6.4, 3.2).
internal static class AccountIdentity
{
    public static async Task<UserEntity> GetCallerAsync(
        VHSmartDbContext db,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
            throw new UnauthorizedException(LoginMessages.InvalidCredentials);

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        if (!user.IsActive)
            throw new ForbiddenException(LoginMessages.Inactive);

        return user;
    }
}
