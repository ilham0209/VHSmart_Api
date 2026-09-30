using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Features.Account.Password;

// Account Setting > Change Password (spec 6.4): verify the old password, store the new hash
// and clear MustChangePassword - the flag exists to force this very action after activation
// (spec 3.2). The spec gives no strength rule (6.4 [VERIFY]), so only "required + the two new
// fields match" is enforced; see Decisions Q13.
public record ChangePasswordCommand(string OldPassword, string NewPassword, string ConfirmPassword)
    : IRequest<ChangePasswordResponse>;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.OldPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.NewPassword).WithMessage("Confirm Password does not match New Password.");
    }
}

public record ChangePasswordResponse(bool Success);

public class ChangePasswordHandler(VHSmartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ChangePasswordCommand, ChangePasswordResponse>
{
    public async Task<ChangePasswordResponse> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await AccountIdentity.GetCallerAsync(db, currentUser, ct);

        var verification = UserPasswordHasher.Verify(user, user.PasswordHash, request.OldPassword);
        if (verification == PasswordVerificationResult.Failed)
            throw new BusinessRuleException("Old password is incorrect.");

        user.PasswordHash = UserPasswordHasher.Hash(user, request.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(ct); // audit fields are stamped by VHSmartDbContext

        return new ChangePasswordResponse(true);
    }
}
