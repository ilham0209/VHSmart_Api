namespace VHSmart_Api.Shared.Infrastructure.Security;

// Identity always comes from the JWT (CodingRules 7.4); handlers never read it from body or URL.
public interface ICurrentUser
{
    string UserId { get; }

    Guid CompanyId { get; }

    bool IsPlatformAdmin { get; }

    bool ViewAllCompanies { get; }
}
