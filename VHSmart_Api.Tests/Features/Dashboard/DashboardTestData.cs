using Microsoft.Extensions.Configuration;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.RawMaterial;

// File-level aliases: the application, certificate and raw-material fixtures all export a
// CompanyA / CreateDbAsync / SeedCompanyAsync pair, so plain "using static" would be
// ambiguous at every call site.
using App = VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;
using Crt = VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;

namespace VHSmart_Api.Tests.Features.Dashboard;

// Dashboard (DS-01) fixtures: ONE company for every row so each tile sees its own data, an
// in-memory config for the D-03 threshold, and self-contained seeders for the pieces the
// other fixtures do not carry (manufacturer country, staff typhoid fields, a fixed creation
// timestamp so "recently added" order is deterministic).
internal static class DashboardTestData
{
    public static readonly Guid Company = Guid.NewGuid();

    public static TestCurrentUser User() => new(Guid.NewGuid().ToString(), Company);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static IConfiguration Config(int? expiringDays = null)
    {
        var values = new Dictionary<string, string?>();
        if (expiringDays is not null)
            values["Halal:ExpiringDays"] = expiringDays.Value.ToString();
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public static Task SeedCompanyAsync(TestableVHSmartDbContext db) =>
        App.SeedCompanyAsync(db, Company);

    public static Task<Guid> SeedApplicationAsync(TestableVHSmartDbContext db) =>
        App.SeedApplicationAsync(db, Company);

    public static Task<Guid> SeedCertificateAsync(
        TestableVHSmartDbContext db,
        Guid applicationId,
        DateOnly? expiryDate = null,
        string certificateNo = "HAL-2026-0001") =>
        Crt.SeedCertificateAsync(db, Company, applicationId, certificateNo, expiryDate: expiryDate);

    public static Task<Guid> SeedCertificateItemAsync(
        TestableVHSmartDbContext db,
        Guid applicationId,
        Guid? productId,
        Guid? certificateId,
        string itemName = "Santan Kicap") =>
        Crt.SeedCertificateItemAsync(
            db, Company, applicationId, itemName, productId: productId, certificateId: certificateId);

    // A material with a manufacturer in the given country; RawMaterialTestData's seeder has
    // no country parameter, so the manufacturer is built here. addedAt pins SysDateCreated
    // (stamped on insert, so it is fixed AFTER the first save) for deterministic ordering.
    public static async Task<Guid> SeedMaterialAsync(
        TestableVHSmartDbContext db,
        string ingredient = "Rice Flour",
        Guid? manufacturerCountryId = null,
        DateTime? addedAt = null,
        Guid? companyId = null,
        bool isDeleted = false)
    {
        var owner = companyId ?? Company;
        var manufacturer = new ManufacturerSupplierEntity
        {
            CompanyId = owner,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = "Santan Foods",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com",
            ManufacturerCountryId = manufacturerCountryId
        };
        db.ManufacturerSuppliers.Add(manufacturer);

        var row = new RawMaterialEntity
        {
            CompanyId = owner,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = Guid.NewGuid(),
            Ingredient = ingredient,
            IngredientCode = $"RM-{Guid.NewGuid():N}",
            IngredientSourceId = Guid.NewGuid(),
            ManufacturerSupplierId = manufacturer.Id,
            IsPackagingMaterial = false,
            IsDeleted = isDeleted
        };
        db.RawMaterials.Add(row);
        await db.SaveChangesAsync();

        if (addedAt is not null)
        {
            row.SysDateCreated = addedAt.Value;
            await db.SaveChangesAsync();
        }

        return row.Id;
    }

    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        string name,
        bool hasTyphoidInjection,
        DateTime? typhoidExpiryDate,
        Guid? companyId = null)
    {
        var row = new StaffEntity
        {
            CompanyId = companyId ?? Company,
            Name = name,
            Email = $"{Guid.NewGuid():N}@example.com",
            TitleId = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            DesignationId = Guid.NewGuid(),
            HasTyphoidInjection = hasTyphoidInjection,
            TyphoidExpiryDate = typhoidExpiryDate
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
