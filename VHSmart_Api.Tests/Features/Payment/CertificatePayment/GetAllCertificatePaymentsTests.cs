using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Payment.CertificatePayment;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Payment.CertificatePayment.CertificatePaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.CertificatePayment;

public class GetAllCertificatePaymentsTests
{
    private static GetAllCertificatePaymentsQuery Query(
        DataGridRequest? request = null,
        string? view = null,
        string? applicationType = null) => new()
        {
            Request = request ?? new DataGridRequest(),
            View = view,
            ApplicationType = applicationType
        };

    [Fact]
    public async Task Handle_ReturnsOwnRowsWithReadThroughColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (schemeId, applicationId, staffId, paymentId) = await SeedStackAsync(db, CompanyA);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(paymentId, row.Id);
        Assert.Equal($"HAL({schemeId:N})/12102026/1", row.ApplicationReferenceNo);
        Assert.Equal("CB-001", row.CbApplicationNo);
        Assert.Equal("Halal Food", row.Scheme);
        Assert.Equal("DRAFT", row.ApplicationStatus);
        Assert.Equal(PaymentStatus.RequestBankDraft, row.PaymentStatus);
        Assert.Equal(500m, row.Amount);
        Assert.Null(row.Brand);
        Assert.Null(row.Batch);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsCreatedDateNewestFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyA);
        var older = await SeedPaymentAsync(db, CompanyA, applicationId, staffId, staffId);
        var newer = await SeedPaymentAsync(db, CompanyA, applicationId, staffId, staffId);

        // Same-second seeds share SysDateCreated to the tick only by luck; force the order.
        var olderRow = await db.Payments.FirstAsync(row => row.Id == older);
        var newerRow = await db.Payments.FirstAsync(row => row.Id == newer);
        olderRow.SysDateCreated = DateTime.UtcNow.AddMinutes(-5);
        newerRow.SysDateCreated = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal([newer, older], grid.Data.Select(row => row.Id));
    }

    [Fact]
    public async Task Handle_ViewFilter_FiltersPaymentStatus()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var request = await SeedStackAsync(db, CompanyA, PaymentStatus.RequestBankDraft);
        var schemeId = await SeedSchemeAsync(db, "Halal Logistics");
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var received = await SeedPaymentAsync(
            db, CompanyA, applicationId, request.StaffId, request.StaffId,
            PaymentStatus.ReceivedBankDraft);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(view: PaymentStatus.ReceivedBankDraft), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal(received, Assert.Single(grid.Data).Id);
    }

    [Fact]
    public async Task Handle_ViewDefault_IsNoFilter()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedStackAsync(db, CompanyA, PaymentStatus.RequestBankDraft);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyA, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyA);
        await SeedPaymentAsync(db, CompanyA, applicationId, staffId, staffId,
            PaymentStatus.ReceivedBankDraft);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(view: "Default"), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_ApplicationTypeFilter_FiltersLinkedApplication()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var renewalApplication = await SeedApplicationAsync(
            db, CompanyA, schemeId, applicationType: "Renewal");
        var newApplication = await SeedApplicationAsync(
            db, CompanyA, schemeId, applicationType: "New");
        var staffId = await SeedStaffAsync(db, CompanyA);
        var renewal = await SeedPaymentAsync(
            db, CompanyA, renewalApplication, staffId, staffId);
        await SeedPaymentAsync(db, CompanyA, newApplication, staffId, staffId);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(applicationType: "Renewal"), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal(renewal, Assert.Single(grid.Data).Id);
    }

    [Fact]
    public async Task Handle_ApplicationTypeAll_IsNoFilter()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var renewalApplication = await SeedApplicationAsync(
            db, CompanyA, schemeId, applicationType: "Renewal");
        var newApplication = await SeedApplicationAsync(
            db, CompanyA, schemeId, applicationType: "New");
        var staffId = await SeedStaffAsync(db, CompanyA);
        await SeedPaymentAsync(db, CompanyA, renewalApplication, staffId, staffId);
        await SeedPaymentAsync(db, CompanyA, newApplication, staffId, staffId);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(applicationType: "All"), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_BatchApplication_ShowsSchemeBatchAndBrand()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var batchId = await SeedBatchAsync(db, CompanyA, schemeId, "BATCH-A");
        var brandName = await SeedBrandAsync(db, CompanyA, schemeId, batchId);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, schemeId, batchId, status: ApplicationStatus.ApplicationApproved);
        var staffId = await SeedStaffAsync(db, CompanyA);
        await SeedPaymentAsync(db, CompanyA, applicationId, staffId, staffId);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        var row = Assert.Single(grid.Data);
        Assert.Equal(brandName, row.Brand);
        Assert.Equal("BATCH-A", row.Batch);
        Assert.Equal("APPLICATION APPROVED", row.ApplicationStatus);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersAcrossColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (schemeId, applicationId, staffId, _) = await SeedStackAsync(db, CompanyA);
        await SeedPaymentAsync(
            db, CompanyA, applicationId, staffId, staffId, paymentVoucherNo: "PV-2026-001");
        var otherApplication = await SeedApplicationAsync(
            db, CompanyA, schemeId, referenceNo: "HAL-OTHER/12102026/2");
        var miss = await SeedPaymentAsync(
            db, CompanyA, otherApplication, staffId, staffId, paymentVoucherNo: "PV-2026-999");

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(new DataGridRequest { SearchTerm = "PV-2026-001" }),
            CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.DoesNotContain(grid.Data, row => row.Id == miss);
    }

    [Fact]
    public async Task Handle_OtherCompany_SeesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var schemeId = await SeedSchemeAsync(db);
        var applicationId = await SeedApplicationAsync(db, CompanyB, schemeId);
        var staffId = await SeedStaffAsync(db, CompanyB);
        await SeedPaymentAsync(db, CompanyB, applicationId, staffId, staffId);

        var grid = await new GetAllCertificatePaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}
