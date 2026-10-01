using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

// Shared fixtures for the Manage Companies suites: a platform user, an in-memory store, and a
// valid command builder (the record has 24 fields, so one builder keeps the test files honest).
internal static class CompanyTestData
{
    public static TestCurrentUser PlatformUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), isPlatformAdmin: true);

    public static TestCurrentUser CompanyUser() =>
        new(Guid.NewGuid().ToString(), Guid.NewGuid());

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

    public static async Task<Guid> SeedCertificationBodyAsync(
        TestableVHSmartDbContext db,
        string name = "Serunai CB")
    {
        var row = new CertificationBodyEntity
        {
            Name = name,
            CountryId = await MalaysiaIdAsync(db),
            City = "Kuala Lumpur",
            Postcode = "50000",
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@cb.example",
            ContactPerson = "Director"
        };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // D-20 source rows live in AdmGeneralData (COMPANY / Brand).
    public static async Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        string name,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? Guid.NewGuid(),
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedCompanyAsync(
        TestableVHSmartDbContext db,
        string name,
        Guid[]? brandIds = null,
        bool isActive = true)
    {
        var company = new CompanyEntity
        {
            Name = name,
            CertificationBodyId = await SeedCertificationBodyAsync(db, $"{name} CB"),
            RegistrationType = "Companies Commission Of Malaysia",
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            OwnerStatus = "Muslim Owner",
            Address1 = "Jalan Perusahaan 1",
            Address2 = "Jalan Perusahaan 2",
            PostCode = "50000",
            District = "Gombak",
            State = "Selangor",
            CountryId = await MalaysiaIdAsync(db),
            Telephone = "0300000000",
            Email = $"{Guid.NewGuid():N}@example.com",
            IsActive = isActive
        };
        db.Companies.Add(company);
        foreach (var brandId in brandIds ?? [])
            db.CompanyBrands.Add(new CompanyBrandEntity
            {
                CompanyId = company.Id,
                BrandId = brandId
            });
        await db.SaveChangesAsync();
        return company.Id;
    }

    public static CreateCompanyCommand ValidCommand(
        Guid countryId,
        Guid certificationBodyId,
        Guid? brandId = null,
        string name = "Acme Foods",
        string? email = null,
        string? businessRegistrationNo = null,
        DateTime? dateOfEstablishment = null,
        Guid? schemeId = null) =>
        new(
            name,
            certificationBodyId,
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
            null,
            null,
            brandId is null ? null : [brandId.Value]);

    public static UpdateCompanyCommand ToUpdate(Guid id, CreateCompanyCommand command) =>
        new(
            id,
            command.Name,
            command.CertificationBodyId,
            command.RegistrationType,
            command.BusinessRegistrationNo,
            command.OwnerStatus,
            command.Address1,
            command.Address2,
            command.Address3,
            command.PostCode,
            command.City,
            command.District,
            command.State,
            command.CountryId,
            command.Telephone,
            command.Fax,
            command.SchemeId,
            command.IndustrySize,
            command.WebsiteUrl,
            command.Email,
            command.DateOfEstablishment,
            command.MainProductsServices,
            command.Market,
            command.IsActive,
            command.BrandIds);
}
