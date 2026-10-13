using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Payment.OtherPayment;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Payment.OtherPayment.OtherPaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

public class GetAllOtherPaymentsTests
{
    private static GetAllOtherPaymentsQuery Query(
        DataGridRequest? request = null,
        string? view = null,
        Guid? paymentCategoryId = null) => new()
        {
            Request = request ?? new DataGridRequest(),
            View = view,
            PaymentCategoryId = paymentCategoryId
        };

    [Fact]
    public async Task Handle_ReturnsOwnRowsWithReadThroughColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (_, _, _, paymentId) = await SeedStackAsync(db, CompanyA);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        var row = Assert.Single(grid.Data);
        Assert.Equal(paymentId, row.Id);
        Assert.Equal("CATEGORY A", row.PaymentCategory);
        Assert.Equal("SERUNAI COMMERCE", row.Payee);
        Assert.Equal("MAYBANK", row.PayeeBankName);
        Assert.Equal("1234-5678", row.PayeeBankAccountNo);
        Assert.Equal(PaymentStatus.RequestBankDraft, row.PaymentStatus);
        Assert.Equal(150m, row.Amount);
    }

    [Fact]
    public async Task Handle_DefaultSort_IsCreatedDateNewestFirst()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA);
        var payeeId = await SeedServiceProviderAsync(db, CompanyA);
        var staffId = await SeedStaffAsync(db, CompanyA);
        var older = await SeedOtherPaymentAsync(db, CompanyA, categoryId, payeeId, staffId);
        var newer = await SeedOtherPaymentAsync(db, CompanyA, categoryId, payeeId, staffId);

        // Same-second seeds share SysDateCreated to the tick only by luck; force the order.
        var olderRow = await db.Payments.FirstAsync(row => row.Id == older);
        var newerRow = await db.Payments.FirstAsync(row => row.Id == newer);
        olderRow.SysDateCreated = DateTime.UtcNow.AddMinutes(-5);
        newerRow.SysDateCreated = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal([newer, older], grid.Data.Select(row => row.Id));
    }

    [Fact]
    public async Task Handle_ViewFilter_FiltersPaymentStatus()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.RequestBankDraft);
        var received = await SeedOtherPaymentAsync(
            db, CompanyA, categoryId, payeeId, staffId, PaymentStatus.ReceivedBankDraft);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(view: PaymentStatus.ReceivedBankDraft), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal(received, Assert.Single(grid.Data).Id);
    }

    [Fact]
    public async Task Handle_ViewDefault_IsNoFilter()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(
            db, CompanyA, PaymentStatus.RequestBankDraft);
        await SeedOtherPaymentAsync(
            db, CompanyA, categoryId, payeeId, staffId, PaymentStatus.ReceivedBankDraft);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(view: "Default"), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_PaymentCategoryFilter_FiltersLinkedCategory()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(db, CompanyA);
        var otherCategoryId = await SeedPaymentCategoryAsync(db, CompanyA, "CATEGORY B");
        var other = await SeedOtherPaymentAsync(
            db, CompanyA, otherCategoryId, payeeId, staffId);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(paymentCategoryId: categoryId), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.DoesNotContain(grid.Data, row => row.Id == other);
    }

    [Fact]
    public async Task Handle_PaymentCategoryAll_IsNoFilter()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(db, CompanyA);
        var otherCategoryId = await SeedPaymentCategoryAsync(db, CompanyA, "CATEGORY B");
        await SeedOtherPaymentAsync(db, CompanyA, otherCategoryId, payeeId, staffId);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(paymentCategoryId: null), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersAcrossColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (categoryId, payeeId, staffId, _) = await SeedStackAsync(db, CompanyA);
        await SeedOtherPaymentAsync(
            db, CompanyA, categoryId, payeeId, staffId, paymentVoucherNo: "ABC");
        var miss = await SeedOtherPaymentAsync(
            db, CompanyA, categoryId, payeeId, staffId, paymentVoucherNo: "XYZ");

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(new DataGridRequest { SearchTerm = "ABC" }),
            CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.DoesNotContain(grid.Data, row => row.Id == miss);
    }

    [Fact]
    public async Task Handle_OtherCompany_SeesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyB);
        var payeeId = await SeedServiceProviderAsync(db, CompanyB);
        var staffId = await SeedStaffAsync(db, CompanyB);
        await SeedOtherPaymentAsync(db, CompanyB, categoryId, payeeId, staffId);

        var grid = await new GetAllOtherPaymentsHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }
}
