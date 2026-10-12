using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

// Shared fixtures for the External Report tests: the D-10 reference data (General Data
// pairs, a certification body, a scheme), premises, and direct report / NC seeders.
// Every test works in its own in-memory database, so the shared company ids are safe to
// reuse.
internal static class ExternalReportTestData
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

    public static async Task<Guid> SeedCertificationBodyAsync(
        TestableVHSmartDbContext db,
        string name = "JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)")
    {
        var row = new CertificationBodyEntity { Name = name };
        db.CertificationBodies.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedSchemeAsync(
        TestableVHSmartDbContext db,
        string name = "Halal Food")
    {
        var row = new SchemeEntity { Name = name, SortOrder = 1 };
        db.Schemes.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedGeneralDataAsync(
        TestableVHSmartDbContext db,
        Guid? companyId,
        string category,
        string name)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId ?? CompanyA,
            Group = GeneralDataGroup.AUDIT,
            Category = category,
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static Task<Guid> SeedTypeOfApplicationAsync(TestableVHSmartDbContext db) =>
        SeedGeneralDataAsync(
            db, CompanyA, "External - Audit Category", "New Application");

    public static Task<Guid> SeedTypeOfInspectionAsync(TestableVHSmartDbContext db) =>
        SeedGeneralDataAsync(
            db, CompanyA, "External - Audit Reference", "Surprise Inspection");

    public static Task<Guid> SeedNonComplianceCategoryAsync(
        TestableVHSmartDbContext db,
        string name = "Critical") =>
        SeedGeneralDataAsync(db, CompanyA, "Non Compliance Category", name);

    public static async Task<Guid> SeedPremiseAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null,
        string name = "Seri Rasa Factory")
    {
        var row = new PremiseEntity
        {
            CompanyId = companyId ?? CompanyA,
            PremiseType = PremiseType.Factory,
            Name = name,
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = "STORE01",
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            District = "Selangor",
            CountryId = ReferenceSeedData.MalaysiaCountryId,
            State = "Selangor",
            Telephone = "0312345678",
            Status = "ACTIVE"
        };
        db.Premises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A ready-to-edit header-form payload plus its ids, so the create/update tests only
    // spell out what they change.
    public static async Task<(
        Guid CertificationBodyId,
        Guid SchemeId,
        string TypeOfApplication,
        string TypeOfInspection,
        Guid PremiseId)> SeedHeaderReferencesAsync(TestableVHSmartDbContext db)
    {
        var certificationBody = await SeedCertificationBodyAsync(db);
        var scheme = await SeedSchemeAsync(db);
        await SeedTypeOfApplicationAsync(db);
        await SeedTypeOfInspectionAsync(db);
        var premise = await SeedPremiseAsync(db);
        return (certificationBody, scheme, "New Application", "Surprise Inspection", premise);
    }

    // A direct report (+ one premise link) without going through the handler - for the NC,
    // attachment, update and delete tests.
    public static async Task<Guid> SeedReportAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid certificationBodyId,
        Guid schemeId,
        IReadOnlyList<Guid>? premiseIds = null,
        string? auditReferenceNo = null,
        string status = ExternalAuditReportStatus.Opened)
    {
        var report = new ExternalAuditReportEntity
        {
            CompanyId = companyId,
            AuditReferenceNo = auditReferenceNo,
            ReceivedFromCertificationBodyId = certificationBodyId,
            CbAuditReferenceNo = "HALAL-20251013-131025(PM)",
            TypeOfApplication = "New Application",
            TypeOfInspection = "Surprise Inspection",
            ApplicationSchemeId = schemeId,
            CbHalalApplicationNo = "APP-001",
            CertificationNo = "CERT-001",
            AuditDate = new DateTime(2025, 11, 29),
            TimeIn = new TimeSpan(9, 30, 0),
            TimeOut = new TimeSpan(12, 15, 0),
            Remarks = "Routine external visit",
            OverallStatus = "Pass",
            Status = status
        };
        db.ExternalAuditReports.Add(report);
        foreach (var premiseId in premiseIds ?? [])
        {
            db.ExternalAuditReportPremises.Add(new ExternalAuditReportPremiseEntity
            {
                CompanyId = companyId,
                ExternalAuditReportId = report.Id,
                PremiseId = premiseId
            });
        }

        await db.SaveChangesAsync();
        return report.Id;
    }

    public static async Task<Guid> SeedNonConformanceAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid reportId,
        Guid? categoryId = null,
        string remarks = "Logged on upload")
    {
        var row = new NonConformanceEntity
        {
            CompanyId = companyId,
            Source = NonConformanceSource.External,
            AuditReferenceNo = null,
            ExternalAuditReportId = reportId,
            ItemName = "Seri Rasa Factory",
            DetailName = "Rendang product",
            Reference = "REF-1",
            Clause = "Clause 2.1",
            NonConformanceDetails = "Pest traces found",
            CategoryId = categoryId,
            Remarks = remarks,
            Status = NonConformanceStatus.Open,
            IsFinal = false
        };
        db.NonConformances.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedAttachmentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid reportId,
        string fileName = "report.pdf")
    {
        var row = new ExternalAuditReportAttachmentEntity
        {
            CompanyId = companyId,
            ExternalAuditReportId = reportId,
            Document = new StoredFile
            {
                FileName = fileName,
                StorageKey = $"{Guid.NewGuid():N}",
                ContentType = "application/pdf",
                SizeBytes = 128
            }
        };
        db.ExternalAuditReportAttachments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
