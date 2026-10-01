using VHSmart_Api.Features.CompanyInformation.General;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Tests.Features.Admin.Companies;

namespace VHSmart_Api.Tests.Features.CompanyInformation.General;

// Fixtures for Company > General: the caller's company row carries the id the JWT will carry,
// so one user instance works for both the handler and the DbContext audit stamps.
internal static class CompanyGeneralTestData
{
    public static TestCurrentUser CompanyUser(Guid? companyId = null, bool viewAllCompanies = false) =>
        new(Guid.NewGuid().ToString(), companyId ?? Guid.NewGuid(), viewAllCompanies: viewAllCompanies);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user) =>
        await CompanyTestData.CreateDbAsync(user);

    // Seeds the caller's own company with Id = user.CompanyId (plus its certification body).
    public static async Task SeedOwnCompanyAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        string name = "Acme Foods",
        bool isActive = true)
    {
        db.Companies.Add(new CompanyEntity
        {
            Id = user.CompanyId,
            Name = name,
            CertificationBodyId = await CompanyTestData.SeedCertificationBodyAsync(db, $"{name} CB"),
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = await CompanyTestData.MalaysiaIdAsync(db),
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com",
            DateOfEstablishment = new DateTime(1990, 6, 15),
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }

    public static async Task<Guid> SeedOtherCompanyAsync(TestableVHSmartDbContext db, string name) =>
        await CompanyTestData.SeedCompanyAsync(db, name);

    public static UpdateCompanyGeneralCommand ValidCommand(
        Guid countryId,
        string name = "Acme Foods",
        string? email = null,
        string? businessRegistrationNo = null,
        DateTime? dateOfEstablishment = null,
        Guid? schemeId = null) =>
        new(
            name,
            "Companies Commission Of Malaysia",
            businessRegistrationNo ?? $"BRN-{Guid.NewGuid():N}",
            "Muslim Owner",
            "Jalan Perusahaan 1",
            "Jalan Perusahaan 2",
            null,
            "50000",
            "Shah Alam",
            "Gombak",
            "Selangor",
            countryId,
            "0300000000",
            null,
            schemeId,
            null,
            null,
            email ?? $"{Guid.NewGuid():N}@example.com",
            dateOfEstablishment ?? new DateTime(1990, 6, 15),
            null,
            null);
}
