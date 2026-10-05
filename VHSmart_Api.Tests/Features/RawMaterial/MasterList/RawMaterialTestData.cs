using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.RawMaterial;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

// Shared fixtures for the Raw Material Master List tests: two companies, their users, the two
// PRODUCT general-data dropdowns, a manufacturer row, a live company row for the "Accessible
// For" picker and a ready-made create command. Every test works in its own in-memory database,
// so the shared company ids are safe to reuse.
internal static class RawMaterialTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> SeedGeneralDataAsync(
        TestableVHSmartDbContext db,
        string category,
        string name,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.PRODUCT,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedManufacturerAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null)
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyA,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = "Santan Foods",
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A live company row so the "Accessible For" picker has a name to show and the validator
    // can find the ids a test sends (spec 6.3 columns are the required ones). Pass an explicit
    // id when the row has to BE the company a token carries (shared-row visibility tests).
    public static async Task<Guid> SeedCompanyAsync(
        TestableVHSmartDbContext db,
        string name,
        Guid? id = null)
    {
        var certificationBody = new CertificationBodyEntity
        {
            Name = $"{name} CB",
            CountryId = await MalaysiaIdAsync(db),
            City = "Kuala Lumpur",
            Postcode = "50000",
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@cb.example",
            ContactPerson = "Director"
        };
        db.CertificationBodies.Add(certificationBody);

        var company = new CompanyEntity
        {
            Name = name,
            CertificationBody = certificationBody,
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
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        if (id is not null)
            company.Id = id.Value;
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    // A raw material of the given owner with its "Accessible For" rows; the two dropdown ids
    // default to fresh rows of company A.
    public static async Task<Guid> SeedRowAsync(
        TestableVHSmartDbContext db,
        string ingredient = "Rice Flour",
        string? ingredientCode = "RM-001",
        Guid? companyId = null,
        Guid? ingredientStatusId = null,
        Guid? ingredientSourceId = null,
        Guid? manufacturerId = null,
        Guid[]? accessibleCompanyIds = null,
        bool isPackagingMaterial = false,
        string? commercialName = null,
        string? scientificName = null)
    {
        var row = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyA,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = ingredientStatusId
                ?? await SeedGeneralDataAsync(db, "Ingredient Status", "Active"),
            Ingredient = ingredient,
            IngredientCode = ingredientCode,
            CommercialName = commercialName,
            ScientificName = scientificName,
            IngredientSourceId = ingredientSourceId
                ?? await SeedGeneralDataAsync(db, "Ingredient Source", "Plant Based"),
            ManufacturerSupplierId = manufacturerId ?? await SeedManufacturerAsync(db),
            IsPackagingMaterial = isPackagingMaterial
        };
        db.RawMaterials.Add(row);

        foreach (var accessibleCompanyId in accessibleCompanyIds ?? [])
            db.RawMaterialAccessibleCompanies.Add(new RawMaterialAccessibleCompanyEntity
            {
                RawMaterialId = row.Id,
                AccessibleCompanyId = accessibleCompanyId
            });

        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<CreateRawMaterialCommand> CommandAsync(
        TestableVHSmartDbContext db,
        RawMaterialCategory? category = RawMaterialCategory.Core,
        Guid? ingredientStatusId = null,
        string? ingredient = "Rice Flour",
        string? ingredientCode = "RM-001",
        string? commercialName = "Rice Flour 1kg",
        string? scientificName = "Oryza sativa",
        Guid? ingredientSourceId = null,
        Guid? manufacturerId = null,
        bool isPackagingMaterial = false,
        IReadOnlyList<Guid>? accessibleCompanyIds = null) =>
        new(
            category,
            ingredientStatusId ?? await SeedGeneralDataAsync(db, "Ingredient Status", "Active"),
            ingredient,
            ingredientCode,
            commercialName,
            scientificName,
            ingredientSourceId ?? await SeedGeneralDataAsync(db, "Ingredient Source", "Plant Based"),
            manufacturerId ?? await SeedManufacturerAsync(db),
            isPackagingMaterial,
            accessibleCompanyIds ?? [await SeedCompanyAsync(db, "Sharing Partner")]);

    public static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
}
