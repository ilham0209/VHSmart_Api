using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Shared fixtures for Premise > Manage Premise (spec 7.7): a company user, an in-memory
// store, seed helpers for every row the premise form references (country, staff, brand,
// prayer room availability) and command builders - the records are long, so one builder
// keeps the test files honest (same approach as CompanyTestData / MinutesMeetingTestData).
internal static class PremiseTestData
{
    public static TestCurrentUser CompanyUser(Guid? companyId = null) =>
        new(Guid.NewGuid().ToString(), companyId ?? Guid.NewGuid());

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    // A second context over the SAME named store - the cross-company tests need two users
    // reading one database (pattern: GetAllMinutesMeetingsTests).
    public static async Task<TestableVHSmartDbContext> CreateDbAsync(
        TestCurrentUser user,
        string databaseName)
    {
        var db = TestDbFactory.Create(databaseName, user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

    // The company row behind a Company cell (the detail joins ComCompanies for the name).
    public static async Task<Guid> SeedCompanyAsync(
        TestableVHSmartDbContext db,
        string name = "Verify Halal Sdn Bhd",
        Guid? companyId = null)
    {
        var row = new CompanyEntity
        {
            Id = companyId ?? Guid.NewGuid(),
            Name = name,
            BusinessRegistrationNo = $"BRN-{Guid.NewGuid():N}",
            Address1 = "1 Jalan Verify",
            Address2 = string.Empty,
            PostCode = "50000",
            District = "Kuala Lumpur",
            State = "Kuala Lumpur",
            Telephone = "0312345678",
            Email = $"{Guid.NewGuid():N}@verify.my"
        };
        db.Companies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A staff row with valid PEOPLE FKs - the source of Premise Manager / Area Manager /
    // Operation Manager / Contact Person picks (spec 7.7: "from All Staff").
    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Siti Aminah",
        string? mobile = "0123456789")
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name,
            Email = $"{Guid.NewGuid():N}@verify.my",
            MobileNumber = mobile,
            TitleId = await AddPeopleValueAsync(db, companyId, "Title of Honour", "Mr"),
            DepartmentId = await AddPeopleValueAsync(db, companyId, "Department", "Production"),
            DesignationId = await AddPeopleValueAsync(db, companyId, "Designation", "Halal Executive")
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // COMPANY / Brand (Database.md 7 links ComPremises.BrandId here) - maintained per
    // company through the Admin General Data screen (Database.md 14, not seeded).
    public static async Task<Guid> SeedBrandAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Seri Rasa")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // COMPANY / Prayer Room Availability (spec 7.7 Facility Information dropdown).
    public static async Task<Guid> SeedPrayerRoomAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Available")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Prayer Room Availability",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // COMPANY / "Premise Tag" (Database.md AdmGeneralData table) - the Tag link values.
    public static async Task<Guid> SeedPremiseTagAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Complete Documentation")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.COMPANY,
            Category = "Premise Tag",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // One ComPremiseAttachments row inserted directly (list/status/download tests). The
    // stored file is a dummy unless the test passes a real one from LocalFileStorage.
    public static async Task<Guid> SeedPremiseAttachmentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid premiseId,
        string documentType,
        DateTime? expiryDate = null,
        string? referenceNo = null,
        StoredFile? document = null)
    {
        var row = new PremiseAttachmentEntity
        {
            CompanyId = companyId,
            PremiseId = premiseId,
            DocumentType = documentType,
            ExpiryDate = expiryDate,
            ReferenceNo = referenceNo,
            Document = document ?? new StoredFile
            {
                FileName = $"{documentType.ToLowerInvariant().Replace(' ', '-')}.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf",
                SizeBytes = 4
            }
        };
        db.PremiseAttachments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A premise inserted directly (a read/update test should not have to go through the
    // save path). Address line 2 is required by the schema, line 3 optional.
    public static async Task<Guid> SeedPremiseAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Seri Rasa Factory",
        string? email = null,
        PremiseType premiseType = PremiseType.Factory,
        string? storeCode = null,
        Guid? areaManagerStaffId = null,
        Guid? operationManagerStaffId = null,
        Guid? prayerRoomAvailabilityId = null,
        string address1 = "1 Jalan Verify",
        string address2 = "Taman Industri",
        string? address3 = null,
        string? city = "Shah Alam")
    {
        var row = new PremiseEntity
        {
            CompanyId = companyId,
            PremiseType = premiseType,
            Name = name,
            Email = email ?? $"{Guid.NewGuid():N}@premise.my",
            StoreCode = storeCode,
            AreaManagerStaffId = areaManagerStaffId,
            OperationManagerStaffId = operationManagerStaffId,
            Address1 = address1,
            Address2 = address2,
            Address3 = address3,
            Postcode = "40000",
            City = city,
            District = "Selangor",
            CountryId = await MalaysiaIdAsync(db),
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE",
            PrayerRoomAvailabilityId = prayerRoomAvailabilityId
        };
        db.Premises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static CreatePremiseCommand ValidCreateCommand(
        Guid countryId,
        PremiseType premiseType = PremiseType.Factory,
        string name = "Seri Rasa Factory",
        string? email = null,
        string? storeCode = null,
        Guid? premiseManagerStaffId = null,
        Guid? areaManagerStaffId = null,
        Guid? prayerRoomAvailabilityId = null) =>
        new(
            premiseType,
            name,
            email ?? $"{Guid.NewGuid():N}@premise.my",
            storeCode,
            premiseManagerStaffId,
            null,
            areaManagerStaffId,
            null,
            "BRN-001",
            null,
            "1 Jalan Verify",
            "Taman Industri",
            null,
            "40000",
            "Shah Alam",
            "Selangor",
            countryId,
            "Selangor",
            "0312345678",
            null,
            null,
            null,
            "ACTIVE",
            prayerRoomAvailabilityId);

    public static UpdatePremiseCommand ValidUpdateCommand(
        Guid id,
        Guid countryId,
        PremiseType premiseType = PremiseType.Factory,
        string name = "Seri Rasa Factory",
        string? email = null,
        string? storeCode = null,
        IReadOnlyList<Guid>? contactStaffIds = null,
        IReadOnlyList<PremiseHostelInput>? hostels = null) =>
        new(
            id,
            premiseType,
            name,
            email ?? $"{Guid.NewGuid():N}@premise.my",
            storeCode,
            null,
            null,
            null,
            null,
            "BRN-001",
            null,
            "1 Jalan Verify",
            "Taman Industri",
            null,
            "40000",
            "Shah Alam",
            "Selangor",
            countryId,
            "Selangor",
            "0312345678",
            null,
            null,
            null,
            "ACTIVE",
            null,
            contactStaffIds,
            hostels);

    private static async Task<Guid> AddPeopleValueAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string category,
        string name)
    {
        var existing = await db.GeneralData
            .FirstOrDefaultAsync(row =>
                row.CompanyId == companyId && row.Category == category && row.Name == name);
        if (existing is not null)
            return existing.Id;

        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PEOPLE,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
