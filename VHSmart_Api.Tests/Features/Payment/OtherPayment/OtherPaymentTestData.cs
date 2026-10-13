using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Payment;

namespace VHSmart_Api.Tests.Features.Payment.OtherPayment;

// Shared fixtures for the Payment > Other tests: direct seeders for service providers, the
// Payment Category general data, staff and Other payments. Every test works in its own
// in-memory database, so the shared company ids are safe to reuse.
internal static class OtherPaymentTestData
{
    public static readonly Guid CompanyA = Guid.NewGuid();

    public static readonly Guid CompanyB = Guid.NewGuid();

    public static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    public static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    public static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(new CompanyEntity { Id = CompanyA, Name = "HALAL CO" });
        db.Companies.Add(new CompanyEntity { Id = CompanyB, Name = "OTHER CO" });
        await db.SaveChangesAsync();
        return db;
    }

    public static async Task<Guid> SeedServiceProviderAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string? name = null,
        string? bankName = null,
        string? bankAccountNo = null)
    {
        var row = new ServiceProviderEntity
        {
            CompanyId = companyId,
            Name = name ?? "SERUNAI COMMERCE",
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{Guid.NewGuid():N}@example.com",
            ContactPerson = "Aminah",
            BankName = bankName,
            BankAccountNo = bankAccountNo
        };
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedPaymentCategoryAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string? name = null)
    {
        var row = new GeneralDataEntity
        {
            CompanyId = companyId,
            Group = GeneralDataGroup.PAYMENT,
            Category = "Payment Category",
            Name = name ?? "CATEGORY A"
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedStaffAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        string? name = null)
    {
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Name = name ?? "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public static async Task<Guid> SeedOtherPaymentAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid paymentCategoryId,
        Guid serviceProviderId,
        Guid staffId,
        string status = PaymentStatus.RequestBankDraft,
        decimal amount = 150m,
        string? paymentVoucherNo = null)
    {
        var row = new OtherPaymentEntity
        {
            CompanyId = companyId,
            PaymentCategoryId = paymentCategoryId,
            ServiceProviderId = serviceProviderId,
            Amount = amount,
            Status = status,
            SubmittedByStaffId = staffId,
            SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReceivedByStaffId = staffId,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentVoucherNo = paymentVoucherNo
        };
        db.Payments.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // A ready Other payment with its category + payee + staff (one shared staff row), for the
    // detail, workflow and list tests.
    public static async Task<(Guid CategoryId, Guid PayeeId, Guid StaffId, Guid PaymentId)>
        SeedStackAsync(
            TestableVHSmartDbContext db,
            Guid companyId,
            string status = PaymentStatus.RequestBankDraft,
            decimal amount = 150m)
    {
        var categoryId = await SeedPaymentCategoryAsync(db, companyId);
        var payeeId = await SeedServiceProviderAsync(
            db, companyId, bankName: "MAYBANK", bankAccountNo: "1234-5678");
        var staffId = await SeedStaffAsync(db, companyId);
        var paymentId = await SeedOtherPaymentAsync(
            db, companyId, categoryId, payeeId, staffId, status, amount);
        return (categoryId, payeeId, staffId, paymentId);
    }
}
