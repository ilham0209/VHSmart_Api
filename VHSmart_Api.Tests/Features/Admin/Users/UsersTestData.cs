using Microsoft.Extensions.Configuration;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Features.Admin.Users;

// Shared fixtures for the Manage Users tests: the three seeded roles arrive with
// EnsureCreated (HasData), companies and users are inserted by hand.
internal static class UsersTestData
{
    public static TestCurrentUser PlatformAdmin(Guid companyId) =>
        new(Guid.NewGuid().ToString(), companyId, RoleSeedData.VhSmartAdminRoleId, isPlatformAdmin: true);

    public static TestCurrentUser CompanyAdmin(Guid companyId) =>
        new(Guid.NewGuid().ToString(), companyId, RoleSeedData.VhSmartAdminRoleId);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(
        string databaseName,
        ICurrentUser? user = null)
    {
        var db = TestDbFactory.Create(databaseName, user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> AddCompanyAsync(VHSmartDbContext db, string name)
    {
        var certificationBody = new CertificationBodyEntity { Name = $"CB {name}" };
        db.CertificationBodies.Add(certificationBody);
        await db.SaveChangesAsync();

        var company = new CompanyEntity
        {
            Name = name,
            CertificationBodyId = certificationBody.Id,
            BusinessRegistrationNo = Guid.NewGuid().ToString("N")[..12],
            Address1 = "Jalan Test 1",
            Address2 = "Jalan Test 2",
            PostCode = "50000",
            District = "Test District",
            State = "Test State",
            CountryId = ReferenceSeedData.MalaysiaCountryId,
            Email = $"{Guid.NewGuid():N}@example.com".ToLowerInvariant(),
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    // An activated, active user so tests that are not about activation can ignore the flow.
    public static async Task<UserEntity> AddUserAsync(
        VHSmartDbContext db,
        string email,
        params Guid[] companyIds)
    {
        var user = new UserEntity
        {
            Name = "TEST USER",
            Email = email.ToLowerInvariant(),
            RoleId = RoleSeedData.VhSmartAdminRoleId,
            IsActive = true,
            ActivatedAt = DateTime.UtcNow
        };
        user.PasswordHash = UserPasswordHasher.Hash(user, "Passw0rd!");
        db.Users.Add(user);

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

        await db.SaveChangesAsync();
        return user;
    }

    // Empty config: the defaults (48-hour activation window, no login lockout overrides).
    public static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
}
