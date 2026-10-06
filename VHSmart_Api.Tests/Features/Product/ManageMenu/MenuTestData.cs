using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.ManageMenu;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Product;
using VHSmart_Api.Shared.Domain.RawMaterial;

namespace VHSmart_Api.Tests.Features.Product.ManageMenu;

// Shared fixtures for the Manage Menu tests: two companies, their users, the form's dropdown
// rows (own-company "Menu Category" General Data), a live company row for the "List of
// Company" picker, raw materials the 7.3 filter shows and a ready-made create command. Every
// test works in its own in-memory database, so the shared company ids are safe to reuse.
internal static class MenuTestData
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
        GeneralDataGroup group,
        string category,
        string name,
        Guid? companyId = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = group,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The Menu Category* dropdown (spec 9.2, COMPANY group per GeneralDataCatalog).
    public static Task<Guid> SeedMenuCategoryAsync(
        TestableVHSmartDbContext db,
        string name = "Permanent",
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.COMPANY, "Menu Category", name, companyId);

    // A row of the wrong dropdown / group, for the validator tests.
    public static Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.COMPANY, "Brand", "Sereni", companyId);

    public static Task<Guid> SeedProductCategoryAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null) =>
        SeedGeneralDataAsync(db, GeneralDataGroup.PRODUCT, "Product Category", "Sauces", companyId);

    public static async Task<Guid> SeedManufacturerAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Santan Foods")
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = companyId ?? CompanyA,
            Type = ManufacturerSupplierType.Both,
            ManufacturerName = name,
            ManufacturerAddress = "Jalan Gombak 1",
            ManufacturerContactNo = "0312345678",
            ManufacturerEmail = $"{Guid.NewGuid():N}@example.com"
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A raw material the caller can see under the CodingRules 7.3 filter (owner, or shared
    // with the caller). The ingredient code is unique per company (D-17), so it is generated.
    public static async Task<Guid> SeedRawMaterialAsync(
        TestableVHSmartDbContext db,
        string ingredient = "Rice Flour",
        Guid? companyId = null,
        Guid[]? accessibleCompanyIds = null)
    {
        var row = new RawMaterialEntity
        {
            CompanyId = companyId ?? CompanyA,
            Category = RawMaterialCategory.Core,
            IngredientStatusId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Status", "Active", companyId),
            Ingredient = ingredient,
            IngredientCode = $"RM-{Guid.NewGuid():N}",
            ScientificName = "Oryza sativa",
            IngredientSourceId = await SeedGeneralDataAsync(
                db, GeneralDataGroup.PRODUCT, "Ingredient Source", "Plant Based", companyId),
            ManufacturerSupplierId = await SeedManufacturerAsync(db, companyId),
            IsPackagingMaterial = false
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

    // A live company row, so the "List of Company" picker has a name to show and the validator
    // can find the ids a test sends (spec 6.3 columns are the required ones). Pass an explicit
    // id when the row has to BE the company a token carries (shared-menu visibility tests).
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

    // A menu of the given owner with its two child lists; the dropdown id and the two required
    // lists default to fresh rows of the same owner.
    public static async Task<Guid> SeedMenuAsync(
        TestableVHSmartDbContext db,
        string name = "Nasi Lemak",
        Guid? companyId = null,
        Guid? categoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? description = "Everyday rice set",
        Guid[]? accessibleCompanyIds = null,
        Guid[]? rawMaterialIds = null,
        string status = MenuStatus.Active)
    {
        var owner = companyId ?? CompanyA;
        var row = new MenuEntity
        {
            CompanyId = owner,
            Name = name,
            CategoryId = categoryId ?? await SeedMenuCategoryAsync(db, companyId: owner),
            StartDate = startDate,
            EndDate = endDate,
            Description = description,
            Status = status
        };
        db.Menus.Add(row);

        foreach (var accessibleCompanyId in accessibleCompanyIds ?? new[] { owner })
            db.MenuAccessibleCompanies.Add(new MenuAccessibleCompanyEntity
            {
                MenuId = row.Id,
                AccessibleCompanyId = accessibleCompanyId
            });

        foreach (var rawMaterialId in rawMaterialIds
                     ?? new[] { await SeedRawMaterialAsync(db, companyId: owner) })
            db.MenuRawMaterials.Add(new MenuRawMaterialEntity
            {
                CompanyId = owner,
                MenuId = row.Id,
                RawMaterialId = rawMaterialId
            });

        await db.SaveChangesAsync();

        // Seeding and acting are two different requests in production: the delete handlers load
        // the menu row only, so the children must not sit in the change tracker here - EF throws
        // "the association ... has been severed" when a required child is tracked while its
        // parent is removed (every FK of Database.md 9 is Restrict).
        db.ChangeTracker.Clear();
        return row.Id;
    }

    public static async Task<CreateMenuCommand> CommandAsync(
        TestableVHSmartDbContext db,
        string? name = "Nasi Lemak",
        Guid? categoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? description = "Everyday rice set",
        IReadOnlyList<Guid>? accessibleCompanyIds = null,
        IReadOnlyList<Guid>? rawMaterialIds = null) =>
        new(
            name,
            categoryId ?? await SeedMenuCategoryAsync(db),
            startDate,
            endDate,
            description,
            accessibleCompanyIds ?? new[] { await SeedCompanyAsync(db, "Sharing Partner") },
            rawMaterialIds ?? new[] { await SeedRawMaterialAsync(db) });

    public static async Task<UpdateMenuCommand> UpdateCommandAsync(
        TestableVHSmartDbContext db,
        Guid id,
        string? name = "Nasi Lemak Updated",
        Guid? categoryId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? description = "Everyday rice set",
        IReadOnlyList<Guid>? accessibleCompanyIds = null,
        IReadOnlyList<Guid>? rawMaterialIds = null)
    {
        var command = await CommandAsync(
            db, name, categoryId, startDate, endDate, description,
            accessibleCompanyIds, rawMaterialIds);
        return new UpdateMenuCommand(
            id,
            command.Name,
            command.CategoryId,
            command.StartDate,
            command.EndDate,
            command.Description,
            command.AccessibleCompanyIds,
            command.RawMaterialIds);
    }

    public static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .AsNoTracking()
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
}
