using VHSmart_Api.Features.Payment.CertificatePayment;
using static VHSmart_Api.Tests.Features.Payment.CertificatePayment.CertificatePaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

public class CertificatePaymentLookupTests
{
    [Fact]
    public async Task ApplicationLookup_ReturnsOwnApplicationsWithReadThroughs()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCertificationBodyAsync(db);
        var schemeId = await SeedSchemeAsync(db);
        var batchId = await SeedBatchAsync(db, CompanyA, schemeId, "BATCH-A");
        var brandName = await SeedBrandAsync(db, CompanyA, schemeId, batchId);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, schemeId, batchId,
            applicationType: "Renewal",
            status: "APPLICATION APPROVED",
            cbApplicationNo: "CB-001");

        var grid = await new GetCertificatePaymentApplicationLookupHandler(db, user).Handle(
            new GetCertificatePaymentApplicationLookupQuery(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(applicationId, row.Id);
        Assert.Equal($"HAL({schemeId:N})/12102026/1", row.ReferenceNo);
        Assert.Equal("CB-001", row.CbApplicationNo);
        Assert.Equal("Halal Food", row.Scheme);
        Assert.Equal("Renewal", row.ApplicationType);
        Assert.Equal(brandName, row.Brand);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", row.CertificationBody);
        Assert.Equal("APPLICATION APPROVED", row.Status);
    }

    [Fact]
    public async Task ApplicationLookup_DefaultSort_IsReferenceNoDescending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var older = await SeedApplicationAsync(
            db, CompanyA, schemeId, referenceNo: "HAL-A/12102026/1");
        var newer = await SeedApplicationAsync(
            db, CompanyA, schemeId, referenceNo: "HAL-B/12102026/2");

        var grid = await new GetCertificatePaymentApplicationLookupHandler(db, user).Handle(
            new GetCertificatePaymentApplicationLookupQuery(), CancellationToken.None);

        Assert.Equal([newer, older], grid.Data.Select(row => row.Id));
    }

    [Fact]
    public async Task ApplicationLookup_SearchTerm_FiltersAcrossColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        await SeedApplicationAsync(
            db, CompanyA, schemeId, referenceNo: "HAL-A/12102026/1", cbApplicationNo: "CB-777");
        var miss = await SeedApplicationAsync(
            db, CompanyA, schemeId, referenceNo: "HAL-B/12102026/2", cbApplicationNo: "CB-888");

        var grid = await new GetCertificatePaymentApplicationLookupHandler(db, user).Handle(
            new GetCertificatePaymentApplicationLookupQuery
            {
                Request = new() { SearchTerm = "CB-777" }
            },
            CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.DoesNotContain(grid.Data, row => row.Id == miss);
    }

    [Fact]
    public async Task ApplicationLookup_OtherCompany_SeesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        await SeedApplicationAsync(db, CompanyB, schemeId);

        var grid = await new GetCertificatePaymentApplicationLookupHandler(db, user).Handle(
            new GetCertificatePaymentApplicationLookupQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }

    [Fact]
    public async Task StaffOptions_ReturnsOwnStaffOrderedByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedStaffAsync(db, CompanyA, "Zaimah Rosli");
        await SeedStaffAsync(db, CompanyA, "Ahmad Faiz");
        await SeedStaffAsync(db, CompanyB, "Foreign Staff");

        var options = await new GetPaymentStaffOptionsHandler(db, user).Handle(
            new GetPaymentStaffOptionsQuery(null), CancellationToken.None);

        Assert.Equal(2, options.Count);
        Assert.Equal(["Ahmad Faiz", "Zaimah Rosli"], options.Select(row => row.Name));
    }

    [Fact]
    public async Task StaffOptions_SearchTerm_FiltersByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedStaffAsync(db, CompanyA, "Zaimah Rosli");
        await SeedStaffAsync(db, CompanyA, "Ahmad Faiz");

        var options = await new GetPaymentStaffOptionsHandler(db, user).Handle(
            new GetPaymentStaffOptionsQuery("Zai"), CancellationToken.None);

        var row = Assert.Single(options);
        Assert.Equal("Zaimah Rosli", row.Name);
    }
}
