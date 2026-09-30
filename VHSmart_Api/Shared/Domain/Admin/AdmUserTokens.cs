namespace VHSmart_Api.Shared.Domain.Admin;

// One-time token for account activation (C-02) and forgot/reset password (A-03). Only the
// hash is stored, never the token itself (CodingRules 8.3) - this replaces the legacy
// "e-mail the password" defect (spec 23, Database.md 5). Nothing issues these rows yet.
public class UserTokenEntity : BaseClass
{
    public Guid UserId { get; set; }

    public UserTokenPurpose Purpose { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}

public enum UserTokenPurpose
{
    Activation,
    PasswordReset
}
