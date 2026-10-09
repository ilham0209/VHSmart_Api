using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Infrastructure.Sequences;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

// Shared fixtures for the My Application tests: two companies, their users, the seeded
// scheme list (Database.md 14 - one code-less scheme for the D-09 block), company rows for
// the list's Company Name join and an application seeder for the list screens. Reference
// numbers are produced by the REAL ReferenceNumberGenerator so the D-09 format is asserted,
// not faked; the default prefix is the service's own placeholder "VHS".
internal static class ApplicationTestData
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

    // The first seeded non-food scheme: "PR" Food and Beverages / Supplement Product.
    public static async Task<Guid> ProductSchemeIdAsync(TestableVHSmartDbContext db) =>
        await db.Schemes.AsNoTracking()
            .Where(row => !row.IsFoodPremise)
            .OrderBy(row => row.SortOrder)
            .Select(row => row.Id)
            .FirstAsync();

    // Abattoirs - the one seeded scheme without a code (Database.md 14, VERIFY default).
    public static async Task<Guid> SchemeWithoutCodeIdAsync(TestableVHSmartDbContext db) =>
        await db.Schemes.AsNoTracking()
            .Where(row => row.Code == null)
            .Select(row => row.Id)
            .SingleAsync();

    // The D-08 correct key: Yes, Yes, No, Yes - the only combination that may create.
    public static CreateApplicationCommand CorrectSurveyCommand(Guid schemeId) =>
        new(
            SchemeId: schemeId,
            ApplicationType: null,
            SurveyReadProcedureManual: true,
            SurveyReadMs1500: true,
            SurveyHandlesProhibited: false,
            SurveyHasIhc: true);

    public static async Task<CreateApplicationCommand> ValidCommandAsync(
        TestableVHSmartDbContext db) =>
        CorrectSurveyCommand(await ProductSchemeIdAsync(db));

    public static ReferenceNumberGenerator CreateGenerator(
        TestableVHSmartDbContext db,
        string? prefix = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ReferencePrefix"] = prefix
            })
            .Build();
        return new ReferenceNumberGenerator(db, configuration);
    }

    public static async Task SeedCompanyAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string name = "Sereni Trading Sdn Bhd")
    {
        db.Companies.Add(new CompanyEntity { Id = companyId, Name = name });
        await db.SaveChangesAsync();
    }

    public static async Task<Guid> SeedApplicationAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string? referenceNo = null,
        Guid? schemeId = null,
        Guid? batchId = null,
        string status = ApplicationStatus.Draft,
        DateTime? statusDate = null,
        string applicationType = "New",
        string? cbApplicationNo = null)
    {
        var row = new ApplicationEntity
        {
            CompanyId = companyId,
            ReferenceNo = referenceNo ?? $"TEST/{Guid.NewGuid():N}",
            ApplicationType = applicationType,
            Status = status,
            StatusDate = statusDate ?? DateTime.UtcNow,
            SchemeId = schemeId ?? await ProductSchemeIdAsync(db),
            BatchId = batchId,
            CbApplicationNo = cbApplicationNo,
            // The D-08 key, as every stored application has it (the gate blocks the rest).
            SurveyReadProcedureManual = true,
            SurveyReadMs1500 = true,
            SurveyHandlesProhibited = false,
            SurveyHasIhc = true
        };
        db.Applications.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A linked batch for the "Scheme (with batch name)" column - the Manage Batch fixtures
    // build it so both features see the same batch shape.
    public static Task<Guid> SeedBatchAsync(TestableVHSmartDbContext db, Guid companyId) =>
        BatchTestData.SeedBatchAsync(db, companyId, name: "Santan Batch Pertama");

    // A stored Additional Information row (the tab reads whatever is in the table, so the
    // read test inserts directly and the save test builds its own).
    public static async Task SeedAdditionalInfoItemAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid applicationId,
        ApplicationAdditionalInfoSection section,
        string optionCode,
        string? freeText = null)
    {
        db.ApplicationAdditionalInfoItems.Add(new ApplicationAdditionalInfoItemEntity
        {
            CompanyId = companyId,
            ApplicationId = applicationId,
            Section = section,
            OptionCode = optionCode,
            FreeText = freeText
        });
        await db.SaveChangesAsync();
    }
}
