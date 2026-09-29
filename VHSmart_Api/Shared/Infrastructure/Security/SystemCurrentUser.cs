namespace VHSmart_Api.Shared.Infrastructure.Security;

// Used when there is no authenticated user: seed data and background jobs (Database.md section 1).
public sealed class SystemCurrentUser : ICurrentUser
{
    public string UserId => "system";

    public Guid CompanyId => Guid.Empty;

    public bool IsPlatformAdmin => false;

    public bool ViewAllCompanies => false;
}
