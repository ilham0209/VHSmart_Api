using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Admin.Users;

// Delete a user (spec 3.2 list carries a delete action): soft delete only - the user row,
// its company memberships and its one-time tokens all go together, so a deleted account can
// never sign in, switch company or be activated again (CodingRules 7.1, Database.md 5).
public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteUserHandler(VHSmartDbContext db, ICurrentUser caller)
    : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var visible = await UserLookup.GetVisibleUserAsync(db, caller, request.Id, ct);

        var user = await db.Users.SingleAsync(row => row.Id == visible.Id, ct);

        // Dependents first: every FK to AdmUsers is DeleteBehavior.Restrict, so removing the
        // user while its memberships/tokens are still tracked would sever a required
        // relationship before SaveChanges gets the chance to soften the delete.
        var memberships = await db.UserCompanies
            .Where(row => row.UserId == user.Id)
            .ToListAsync(ct);
        db.UserCompanies.RemoveRange(memberships);

        var tokens = await db.UserTokens
            .Where(row => row.UserId == user.Id)
            .ToListAsync(ct);
        db.UserTokens.RemoveRange(tokens);

        db.Users.Remove(user);

        await db.SaveChangesAsync(ct);
    }
}
