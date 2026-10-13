using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.Payment;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.ServiceProviders;

public class DeleteServiceProviderTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ServiceProviderEntity NewRow(Guid companyId, string name) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = "provider@example.com",
            ContactPerson = "Aminah"
        };

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndClearsTheName()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();

        await new DeleteServiceProviderHandler(db)
            .Handle(new DeleteServiceProviderCommand(row.Id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.ServiceProviders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.ServiceProviders.ToArrayAsync());

        // The name is free again for a new live row.
        Assert.False(await db.ServiceProviders.AnyAsync(provider => provider.Name == "Santan Provider"));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteServiceProviderHandler(db)
                .Handle(new DeleteServiceProviderCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B payee");
        db.ServiceProviders.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteServiceProviderHandler(db)
                .Handle(new DeleteServiceProviderCommand(foreignRow.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();

        await new DeleteServiceProviderHandler(db)
            .Handle(new DeleteServiceProviderCommand(row.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteServiceProviderHandler(db)
                .Handle(new DeleteServiceProviderCommand(row.Id), CancellationToken.None));
    }

    // PY-02 guard (spec 5.3 "delete only when unused", deferred from R-04): a provider that
    // is the Payee of a live Payment > Other row cannot be deleted.
    [Fact]
    public async Task Handle_UsedByLiveOtherPayment_ThrowsConflict()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var payee = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(payee);

        var category = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.PAYMENT,
            Category = "Payment Category",
            Name = "CATEGORY A"
        };
        db.GeneralData.Add(category);

        var staff = new StaffEntity
        {
            CompanyId = CompanyA,
            Name = "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();

        db.Payments.Add(new OtherPaymentEntity
        {
            CompanyId = CompanyA,
            PaymentCategoryId = category.Id,
            ServiceProviderId = payee.Id,
            Amount = 150m,
            SubmittedByStaffId = staff.Id,
            SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReceivedByStaffId = staff.Id,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteServiceProviderHandler(db)
                .Handle(new DeleteServiceProviderCommand(payee.Id), CancellationToken.None));

        Assert.Equal(
            "This service provider is used by a payment and cannot be deleted.",
            exception.Message);
        Assert.False(await db.ServiceProviders.IgnoreQueryFilters()
            .Where(row => row.Id == payee.Id)
            .Select(row => row.IsDeleted)
            .SingleAsync());
    }

    [Fact]
    public async Task Handle_UsedOnlyBySoftDeletedPayment_SoftDeletes()
    {
        // Two contexts on the same store: a real delete request never carries the already
        // soft-deleted payment in its change tracker (the global filter hides it).
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var payee = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(payee);

        var category = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.PAYMENT,
            Category = "Payment Category",
            Name = "CATEGORY A"
        };
        db.GeneralData.Add(category);

        var staff = new StaffEntity
        {
            CompanyId = CompanyA,
            Name = "Staff One",
            Email = $"{Guid.NewGuid():N}@example.com"
        };
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();

        var payment = new OtherPaymentEntity
        {
            CompanyId = CompanyA,
            PaymentCategoryId = category.Id,
            ServiceProviderId = payee.Id,
            Amount = 150m,
            SubmittedByStaffId = staff.Id,
            SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReceivedByStaffId = staff.Id,
            ReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        db.Payments.Remove(payment);
        await db.SaveChangesAsync();

        var freshDb = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await new DeleteServiceProviderHandler(freshDb)
            .Handle(new DeleteServiceProviderCommand(payee.Id), CancellationToken.None);

        var stored = await freshDb.ServiceProviders.IgnoreQueryFilters()
            .FirstAsync(row => row.Id == payee.Id);
        Assert.True(stored.IsDeleted);
    }
}
