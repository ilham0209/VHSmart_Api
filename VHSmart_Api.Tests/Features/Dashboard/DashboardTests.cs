using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Dashboard;
using VHSmart_Api.Shared.Domain.Calculators;
using static VHSmart_Api.Tests.Features.Dashboard.DashboardTestData;

using App = VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;
using Crt = VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using Raw = VHSmart_Api.Tests.Features.RawMaterial.MasterList.RawMaterialTestData;

namespace VHSmart_Api.Tests.Features.Dashboard;

// Spec 13: the six summary tiles (D-03 threshold from config, D-04 expiry rules), the
// recently-added panel, the typhoid panel and the raw-material-by-country list.
public class DashboardTests
{
    private static readonly DateOnly InThirtyDays = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task Summary_CountsTheSixTilesPerD03AndD04()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db);
        var applicationId = await SeedApplicationAsync(db);

        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "EXP-PAST");
        await SeedCertificateAsync(db, applicationId, InThirtyDays, "EXP-SOON");
        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(200)), "VALID");
        await SeedCertificateAsync(db, applicationId, null, "NO-EXPIRY");
        await SeedCertificateAsync(db, applicationId, HalalStatusCalculator.NoExpiryDate, "SENTINEL");

        // Distinct products WITH a certificate-linked item = 1; the same product twice, an
        // item without a certificate and a certificate item without a product do not count.
        var certifiedProduct = Guid.NewGuid();
        await SeedCertificateItemAsync(db, applicationId, certifiedProduct, Guid.NewGuid());
        await SeedCertificateItemAsync(db, applicationId, certifiedProduct, Guid.NewGuid());
        await SeedCertificateItemAsync(db, applicationId, Guid.NewGuid(), null);
        await SeedCertificateItemAsync(db, applicationId, null, Guid.NewGuid());

        var halalType = await Raw.SeedSupportingDocumentAsync(db, "HALAL CERTIFICATE", companyId: Company);
        var otherType = await Raw.SeedSupportingDocumentAsync(
            db, "PROCESS FLOW", companyId: Company, documentSequence: 2);

        var expiredCert = await SeedMaterialAsync(db, "Expired Cert");
        await Raw.SeedAttachmentAsync(db, expiredCert, halalType,
            companyId: Company, expiryDate: DateTime.UtcNow.AddDays(-1));
        var expiringCert = await SeedMaterialAsync(db, "Expiring Cert");
        await Raw.SeedAttachmentAsync(db, expiringCert, halalType,
            companyId: Company, expiryDate: DateTime.UtcNow.AddDays(30));
        await SeedMaterialAsync(db, "No Cert");
        var wrongDocType = await SeedMaterialAsync(db, "Wrong Doc Type");
        await Raw.SeedAttachmentAsync(db, wrongDocType, otherType,
            companyId: Company, expiryDate: DateTime.UtcNow.AddDays(-1));

        var response = await new GetDashboardSummaryHandler(db, user, Config())
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(5, response.TotalHalalCertificate);
        Assert.Equal(1, response.TotalExpiringHalalCertificate);
        Assert.Equal(1, response.TotalExpiredCertificate);
        Assert.Equal(1, response.TotalCertifiedProducts);
        Assert.Equal(4, response.TotalRawMaterial);
        Assert.Equal(2, response.TotalExpiringAndExpiredRawMaterialHalalCertificate);
    }

    [Fact]
    public async Task Summary_UsesTheConfiguredExpiringDays()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        var applicationId = await SeedApplicationAsync(db);
        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "SOON");
        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), "LATER");

        var response = await new GetDashboardSummaryHandler(db, user, Config(expiringDays: 10))
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(2, response.TotalHalalCertificate);
        Assert.Equal(1, response.TotalExpiringHalalCertificate);
        Assert.Equal(0, response.TotalExpiredCertificate);
    }

    [Fact]
    public async Task Summary_FallsBackToNinetyDaysWhenTheSettingIsMissing()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        var applicationId = await SeedApplicationAsync(db);
        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), "SIXTY");
        await SeedCertificateAsync(db, applicationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(120)), "HUNDRED-TWENTY");

        var response = await new GetDashboardSummaryHandler(db, user, Config())
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(2, response.TotalHalalCertificate);
        Assert.Equal(1, response.TotalExpiringHalalCertificate);
    }

    [Fact]
    public async Task Summary_ExcludesRowsOfAnotherCompany()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        var otherCompany = Guid.NewGuid();
        var applicationId = await App.SeedApplicationAsync(db, otherCompany);
        await Crt.SeedCertificateAsync(db, otherCompany, applicationId,
            "OTHER", expiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        await SeedMaterialAsync(db, "Other Material", companyId: otherCompany);

        var response = await new GetDashboardSummaryHandler(db, user, Config())
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.Equal(0, response.TotalHalalCertificate);
        Assert.Equal(0, response.TotalRawMaterial);
        Assert.Equal(0, response.TotalCertifiedProducts);
    }

    [Fact]
    public async Task RecentlyAdded_ReturnsNewestFirstWithCompanyAndStopsAtLimit()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db);
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedMaterialAsync(db, "First", addedAt: baseDate);
        await SeedMaterialAsync(db, "Third", addedAt: baseDate.AddDays(2));
        var secondId = await SeedMaterialAsync(db, "Second", addedAt: baseDate.AddDays(1));

        var rows = await new GetRecentlyAddedRawMaterialsHandler(db)
            .Handle(new GetRecentlyAddedRawMaterialsQuery(Limit: 2), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Third", rows[0].Ingredient);
        Assert.Equal("Second", rows[1].Ingredient);
        Assert.Equal(secondId, rows[1].Id);
        Assert.Equal("Sereni Trading Sdn Bhd", rows[1].Company);
        Assert.Equal(baseDate.AddDays(1), rows[1].AddedAt);
    }

    [Fact]
    public async Task RecentlyAdded_ExcludesOtherCompaniesAndDeletedRows()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db);
        await SeedMaterialAsync(db, "Foreign", companyId: Guid.NewGuid());
        await SeedMaterialAsync(db, "Deleted", isDeleted: true);
        await SeedMaterialAsync(db, "Mine");

        var rows = await new GetRecentlyAddedRawMaterialsHandler(db)
            .Handle(new GetRecentlyAddedRawMaterialsQuery(), CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("Mine", row.Ingredient);
    }

    [Fact]
    public void RecentlyAdded_LimitOutsideTheAllowedRange_FailsValidation()
    {
        var validator = new GetRecentlyAddedRawMaterialsValidator();

        Assert.False(validator.Validate(new GetRecentlyAddedRawMaterialsQuery(Limit: 0)).IsValid);
        Assert.False(validator.Validate(new GetRecentlyAddedRawMaterialsQuery(Limit: 51)).IsValid);
        Assert.True(validator.Validate(new GetRecentlyAddedRawMaterialsQuery(Limit: 1)).IsValid);
        Assert.True(validator.Validate(new GetRecentlyAddedRawMaterialsQuery(Limit: 50)).IsValid);
    }

    [Fact]
    public async Task ExpiringTyphoid_CountsOnlyRowsInsideTheWindowSoonestFirst()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        await SeedStaffAsync(db, "Soon", true, DateTime.UtcNow.AddDays(5));
        await SeedStaffAsync(db, "Later", true, DateTime.UtcNow.AddDays(40));
        await SeedStaffAsync(db, "Lapsed", true, DateTime.UtcNow.AddDays(-5));
        await SeedStaffAsync(db, "Beyond", true, DateTime.UtcNow.AddDays(200));
        await SeedStaffAsync(db, "No Injection", false, DateTime.UtcNow.AddDays(5));
        await SeedStaffAsync(db, "No Date", true, null);
        await SeedStaffAsync(db, "Foreign", true, DateTime.UtcNow.AddDays(5), Guid.NewGuid());

        var response = await new GetExpiringTyphoidInjectionHandler(db, user, Config())
            .Handle(new GetExpiringTyphoidInjectionQuery(), CancellationToken.None);

        Assert.Equal(2, response.Total);
        Assert.Equal(
            new[] { "Soon", "Later" },
            response.Items.Select(item => item.Name).ToArray());
        Assert.All(response.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Email)));
    }

    [Fact]
    public async Task ExpiringTyphoid_UsesTheConfiguredWindow()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        await SeedStaffAsync(db, "Soon", true, DateTime.UtcNow.AddDays(5));
        await SeedStaffAsync(db, "Later", true, DateTime.UtcNow.AddDays(20));

        var response = await new GetExpiringTyphoidInjectionHandler(db, user, Config(expiringDays: 10))
            .Handle(new GetExpiringTyphoidInjectionQuery(), CancellationToken.None);

        Assert.Equal(1, response.Total);
        Assert.Equal("Soon", response.Items[0].Name);
    }

    [Fact]
    public async Task ByCountry_GroupsByManufacturerCountryWithOthersBucket()
    {
        var user = User();
        var db = await CreateDbAsync(user);
        var malaysia = await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();
        var albania = await db.Countries
            .Where(country => country.IsoCode == "ALB")
            .Select(country => country.Id)
            .SingleAsync();

        await SeedMaterialAsync(db, "Rice", manufacturerCountryId: malaysia);
        await SeedMaterialAsync(db, "Sugar", manufacturerCountryId: malaysia);
        await SeedMaterialAsync(db, "Honey", manufacturerCountryId: albania);
        await SeedMaterialAsync(db, "Salt");
        await SeedMaterialAsync(db, "Foreign Rice", companyId: Guid.NewGuid(),
            manufacturerCountryId: malaysia);

        var rows = await new GetRawMaterialByCountryHandler(db)
            .Handle(new GetRawMaterialByCountryQuery(), CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.Equal(("Malaysia", 2), (rows[0].Country, rows[0].Count));
        Assert.Equal(("Albania", 1), (rows[1].Country, rows[1].Count));
        Assert.Equal(("Others", 1), (rows[2].Country, rows[2].Count));
    }
}
