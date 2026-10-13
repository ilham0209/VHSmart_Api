using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Product;

// File-level aliases: the application, batch, certificate and premise fixtures all export a
// CompanyA / CreateDbAsync / SeedCompanyAsync pair, so plain "using static" would be
// ambiguous at every call site.
using App = VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;
using Batch = VHSmart_Api.Tests.Features.HalalApplication.ManageBatch.BatchTestData;
using Crt = VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using Prem = VHSmart_Api.Tests.Features.Premise.ManagePremise.PremiseTestData;

namespace VHSmart_Api.Tests.Features.AdvancedSearch;

// Advanced Search > Premise (DS-02) fixtures: one own company plus a foreign tenant, a
// self-contained premise seeder that carries every field the 19 filters read (the Premise
// fixture has no brand / tag / status / date parameters), and one seeded "world" the search
// tests share - four premises covering each filter value, both document-status shapes and a
// premise -> batch -> application -> certificate chain per halal filter.
internal static class PremiseSearchTestData
{
    public static readonly Guid Company = Guid.NewGuid();

    public static readonly Guid ForeignCompany = Guid.NewGuid();

    public static TestCurrentUser User() => new(Guid.NewGuid().ToString(), Company);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    // A premise row carrying every filterable field. The email is unique (UQ CompanyId +
    // Email) as is the store code (UQ CompanyId + StoreCode among live rows).
    public static async Task<Guid> SeedPremiseAsync(
        TestableVHSmartDbContext db,
        string name,
        string? storeCode = null,
        Guid? brandId = null,
        Guid? menuConceptId = null,
        Guid? areaManagerStaffId = null,
        Guid? operationManagerStaffId = null,
        string status = "ACTIVE",
        Guid? tagId = null,
        string state = "Selangor",
        DateTime? openingDate = null,
        DateTime? closingDate = null,
        string? googleMapLink = null,
        Guid? companyId = null)
    {
        var row = new PremiseEntity
        {
            CompanyId = companyId ?? Company,
            PremiseType = PremiseType.Factory,
            Name = name,
            Email = $"{Guid.NewGuid():N}@premise.my",
            StoreCode = storeCode,
            AreaManagerStaffId = areaManagerStaffId,
            OperationManagerStaffId = operationManagerStaffId,
            BrandId = brandId,
            MenuConceptId = menuConceptId,
            TagId = tagId,
            Address1 = "1 Jalan Verify",
            Address2 = "Taman Industri",
            Postcode = "40000",
            City = "Shah Alam",
            District = "Selangor",
            CountryId = await Prem.MalaysiaIdAsync(db),
            State = state,
            Telephone = "0312345678",
            Status = status,
            OpeningDate = openingDate,
            ClosingDate = closingDate,
            GoogleMapLink = googleMapLink
        };
        db.Premises.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedBrandAsync(TestableVHSmartDbContext db, string name)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = Company,
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedPremiseTagAsync(TestableVHSmartDbContext db, string name)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = Company,
            Group = GeneralDataGroup.COMPANY,
            Category = "Premise Tag",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedMenuConceptAsync(
        TestableVHSmartDbContext db,
        string name,
        Guid? companyId = null)
    {
        var row = new MenuConceptEntity
        {
            CompanyId = companyId ?? Company,
            Name = name
        };
        db.MenuConcepts.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // The five D-15 required types with an upload each; expiry picks which rows the
    // calculator counts as expired.
    public static async Task SeedAttachmentsAsync(
        TestableVHSmartDbContext db,
        Guid premiseId,
        IEnumerable<string> expiredDocumentTypes)
    {
        foreach (var documentType in PremiseDocumentStatusCalculator.RequiredDocumentTypes)
        {
            await Prem.SeedPremiseAttachmentAsync(
                db,
                Company,
                premiseId,
                documentType,
                expiryDate: expiredDocumentTypes.Contains(documentType)
                    ? DateTime.UtcNow.AddDays(-30)
                    : DateTime.UtcNow.AddDays(30));
        }
    }

    // Premise -> batch -> application -> certificate (Database.md 12): the only path a
    // premise reaches its halal information. The batch takes an explicit brand so the seeder
    // does not create its own default "Sereni" rows behind the options test's back.
    public static async Task<(Guid BatchId, Guid ApplicationId)> SeedHalalChainAsync(
        TestableVHSmartDbContext db,
        Guid premiseId,
        Guid brandId,
        string cbReferenceNo,
        string referenceNo,
        string applicationStatus,
        string certificateNo,
        DateOnly? expiryDate)
    {
        var batchId = await Batch.SeedBatchAsync(
            db, Company, brandId: brandId, cbReferenceNo: cbReferenceNo);
        await Batch.SeedBatchPremiseAsync(db, Company, batchId, premiseId);
        var applicationId = await App.SeedApplicationAsync(
            db,
            Company,
            referenceNo: referenceNo,
            batchId: batchId,
            status: applicationStatus);
        await Crt.SeedCertificateAsync(
            db, Company, applicationId, certificateNo: certificateNo, expiryDate: expiryDate);
        return (batchId, applicationId);
    }

    public static async Task<SearchWorld> SeedSearchWorldAsync(TestableVHSmartDbContext db)
    {
        // Lookups: two of each so an exact-match filter has something to choose between,
        // plus a foreign company's concept the options endpoint must not offer.
        var brandSeriRasa = await SeedBrandAsync(db, "Seri Rasa");
        var brandKopiLine = await SeedBrandAsync(db, "Kopi Line");
        var tagComplete = await SeedPremiseTagAsync(db, "Complete Documentation");
        var tagPartial = await SeedPremiseTagAsync(db, "Partial Documentation");
        var conceptSereniDining = await SeedMenuConceptAsync(db, "Sereni Dining");
        var conceptKopiBar = await SeedMenuConceptAsync(db, "Kopi Bar");
        await SeedMenuConceptAsync(db, "Foreign Concept", ForeignCompany);
        var areaManager = await Prem.SeedStaffAsync(db, Company, name: "Amy Rashid");
        var operationManager = await Prem.SeedStaffAsync(db, Company, name: "Bob Tan");

        // One premise per combination of premise-owned filter values. Gamma and Delta have
        // no halal chain (their halal filters must stay empty); Alpha / Beta carry none of
        // the attachment sets (NOT COMPLETE) until the rows below.
        var alpha = await SeedPremiseAsync(
            db,
            "Seri Rasa Factory",
            storeCode: "SR-001",
            brandId: brandSeriRasa,
            menuConceptId: conceptSereniDining,
            areaManagerStaffId: areaManager,
            operationManagerStaffId: operationManager,
            status: "ACTIVE",
            tagId: tagComplete,
            state: "Selangor",
            openingDate: new DateTime(2026, 6, 15),
            closingDate: new DateTime(2026, 11, 30),
            googleMapLink: "https://maps.example/alpha");
        var beta = await SeedPremiseAsync(
            db,
            "Kedai Kopi Dummy",
            storeCode: "KO-002",
            brandId: brandKopiLine,
            menuConceptId: conceptKopiBar,
            status: "INACTIVE",
            tagId: tagPartial,
            state: "Johor",
            openingDate: new DateTime(2025, 3, 1));
        var gamma = await SeedPremiseAsync(db, "Gamma Cafe", storeCode: "GC-003");
        var delta = await SeedPremiseAsync(db, "Delta Restoran", storeCode: "DR-004");

        // Document statuses: Gamma = five uploaded, none expired (COMPLETE); Delta = five
        // uploaded, two expired ("2 OF THE DOCUMENT HAS EXPIRED"); Alpha / Beta = no rows
        // at all (NOT COMPLETE).
        await SeedAttachmentsAsync(db, gamma, []);
        await SeedAttachmentsAsync(db, delta, PremiseDocumentStatusCalculator.RequiredDocumentTypes.Take(2));

        // Fixed dates (the house style of the halal tests): Alpha's certificate is valid
        // beyond today, Beta's expired long before - D-04 / D-26 values throughout.
        var (alphaBatch, alphaApplication) = await SeedHalalChainAsync(
            db,
            alpha,
            brandId: brandSeriRasa,
            cbReferenceNo: "CB-001",
            referenceNo: "VHS/2026/0001",
            applicationStatus: ApplicationStatus.ApplicationApproved,
            certificateNo: "HAL-VALID-001",
            expiryDate: new DateOnly(2027, 6, 30));
        var (betaBatch, betaApplication) = await SeedHalalChainAsync(
            db,
            beta,
            brandId: brandKopiLine,
            cbReferenceNo: "CB-002",
            referenceNo: "VHS/2026/0002",
            applicationStatus: ApplicationStatus.Draft,
            certificateNo: "HAL-EXPIRED-001",
            expiryDate: new DateOnly(2024, 1, 15));

        // A foreign tenant's premise matching most own-company filters - it must never show.
        var foreignPremise = await SeedPremiseAsync(
            db, "Foreign premise", storeCode: "FX-999", companyId: ForeignCompany);

        return new SearchWorld(
            alpha,
            beta,
            gamma,
            delta,
            foreignPremise,
            brandSeriRasa,
            brandKopiLine,
            conceptSereniDining,
            conceptKopiBar,
            tagComplete,
            tagPartial,
            areaManager,
            operationManager,
            alphaBatch,
            betaBatch,
            alphaApplication,
            betaApplication);
    }
}

// The ids the search tests assert against - the premises carry the distinct filter values,
// the lookup rows feed the dropdown filters and the options endpoint.
internal sealed record SearchWorld(
    Guid Alpha,
    Guid Beta,
    Guid Gamma,
    Guid Delta,
    Guid ForeignPremise,
    Guid BrandSeriRasa,
    Guid BrandKopiLine,
    Guid ConceptSereniDining,
    Guid ConceptKopiBar,
    Guid TagCompleteDocumentation,
    Guid TagPartialDocumentation,
    Guid AreaManager,
    Guid OperationManager,
    Guid AlphaBatch,
    Guid BetaBatch,
    Guid AlphaApplication,
    Guid BetaApplication);
