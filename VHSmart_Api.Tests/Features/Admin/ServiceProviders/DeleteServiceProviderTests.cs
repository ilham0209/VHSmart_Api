using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Domain.Admin;
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
}
