using VHSmart_Api.Features.Payment.OtherPayment;
using VHSmart_Api.Shared.Domain.Admin;
using static VHSmart_Api.Tests.Features.Payment.OtherPayment.OtherPaymentTestData;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

public class OtherPaymentLookupTests
{
    [Fact]
    public async Task PayeeOptions_ReturnsOwnProvidersWithBankDetails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var payeeId = await SeedServiceProviderAsync(
            db, CompanyA, "SERUNAI COMMERCE", "MAYBANK", "1234-5678");
        await SeedServiceProviderAsync(db, CompanyB, "Foreign Provider");

        var options = await new GetOtherPaymentPayeeOptionsHandler(db, user).Handle(
            new GetOtherPaymentPayeeOptionsQuery(null), CancellationToken.None);

        var row = Assert.Single(options);
        Assert.Equal(payeeId, row.Id);
        Assert.Equal("SERUNAI COMMERCE", row.Name);
        Assert.Equal("MAYBANK", row.BankName);
        Assert.Equal("1234-5678", row.BankAccountNo);
    }

    [Fact]
    public async Task PayeeOptions_DefaultSort_IsNameAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedServiceProviderAsync(db, CompanyA, "Zaimah Supplies");
        await SeedServiceProviderAsync(db, CompanyA, "Ahmad Foods");

        var options = await new GetOtherPaymentPayeeOptionsHandler(db, user).Handle(
            new GetOtherPaymentPayeeOptionsQuery(null), CancellationToken.None);

        Assert.Equal(["Ahmad Foods", "Zaimah Supplies"], options.Select(row => row.Name));
    }

    [Fact]
    public async Task PayeeOptions_SearchTerm_FiltersByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedServiceProviderAsync(db, CompanyA, "Zaimah Supplies");
        await SeedServiceProviderAsync(db, CompanyA, "Ahmad Foods");

        var options = await new GetOtherPaymentPayeeOptionsHandler(db, user).Handle(
            new GetOtherPaymentPayeeOptionsQuery("Zai"), CancellationToken.None);

        var row = Assert.Single(options);
        Assert.Equal("Zaimah Supplies", row.Name);
    }

    [Fact]
    public async Task CategoryOptions_ReturnsOwnPaymentCategoriesOnly()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedPaymentCategoryAsync(db, CompanyA, "CATEGORY A");
        await SeedPaymentCategoryAsync(db, CompanyA, "CATEGORY B");
        await SeedPaymentCategoryAsync(db, CompanyB, "FOREIGN CATEGORY");

        // A row of another Group must not leak into the payment dropdown.
        db.GeneralData.Add(new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.PRODUCT,
            Category = "Product Category",
            Name = "Sauce"
        });
        await db.SaveChangesAsync();

        var options = await new GetOtherPaymentCategoryOptionsHandler(db, user).Handle(
            new GetOtherPaymentCategoryOptionsQuery(), CancellationToken.None);

        Assert.Equal(2, options.Count);
        Assert.Equal(["CATEGORY A", "CATEGORY B"], options.Select(row => row.Name));
        Assert.Contains(options, row => row.Id == categoryId);
        Assert.DoesNotContain(options, row => row.Name == "Sauce");
    }

    [Fact]
    public async Task StaffOptions_ReturnsOwnStaffOrderedByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedStaffAsync(db, CompanyA, "Zaimah Rosli");
        await SeedStaffAsync(db, CompanyA, "Ahmad Faiz");
        await SeedStaffAsync(db, CompanyB, "Foreign Staff");

        var options = await new GetOtherPaymentStaffOptionsHandler(db, user).Handle(
            new GetOtherPaymentStaffOptionsQuery(null), CancellationToken.None);

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

        var options = await new GetOtherPaymentStaffOptionsHandler(db, user).Handle(
            new GetOtherPaymentStaffOptionsQuery("Zai"), CancellationToken.None);

        var row = Assert.Single(options);
        Assert.Equal("Zaimah Rosli", row.Name);
    }
}
