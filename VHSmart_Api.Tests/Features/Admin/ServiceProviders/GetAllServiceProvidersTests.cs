using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.ServiceProviders;

public class GetAllServiceProvidersTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static TestCurrentUser UserB() => new(Guid.NewGuid().ToString(), CompanyB);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ServiceProviderEntity NewRow(
        Guid companyId,
        string name,
        string? description = null) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Description = description,
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Email = $"{name}@example.com",
            ContactPerson = "Contact"
        };

    [Fact]
    public async Task Handle_SearchTerm_FindsNameOrDescription()
    {
        var db = await CreateDbAsync(UserA());
        db.ServiceProviders.AddRange(
            NewRow(CompanyA, "Santan Provider"),
            NewRow(CompanyA, "CIMB", "corporate banking payee"),
            NewRow(CompanyA, "Maybank"));
        await db.SaveChangesAsync();

        var byName = await new GetAllServiceProvidersHandler(db)
            .Handle(
                new GetAllServiceProvidersQuery { Request = new DataGridRequest { SearchTerm = "Santan" } },
                CancellationToken.None);
        var byDescription = await new GetAllServiceProvidersHandler(db)
            .Handle(
                new GetAllServiceProvidersQuery { Request = new DataGridRequest { SearchTerm = "banking" } },
                CancellationToken.None);

        Assert.Equal("Santan Provider", Assert.Single(byName.Data).Name);
        Assert.Equal("CIMB", Assert.Single(byDescription.Data).Name);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.ServiceProviders.AddRange(
            NewRow(CompanyA, "Company A payee"),
            NewRow(CompanyB, "Company B payee"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllServiceProvidersHandler(db)
            .Handle(new GetAllServiceProvidersQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyA.TotalRecords);
        Assert.Equal("Company A payee", Assert.Single(asCompanyA.Data).Name);

        // A second context for company B on the same store sees only its own row.
        var dbB = TestDbFactory.Create(databaseName, UserB());
        var asCompanyB = await new GetAllServiceProvidersHandler(dbB)
            .Handle(new GetAllServiceProvidersQuery(), CancellationToken.None);

        Assert.Equal(1, asCompanyB.TotalRecords);
        Assert.Equal("Company B payee", Assert.Single(asCompanyB.Data).Name);
    }

    [Fact]
    public async Task Handle_SortBySysDateModified_NewestFirst()
    {
        var db = await CreateDbAsync(UserA());
        var older = NewRow(CompanyA, "Older payee");
        older.SysDateModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = NewRow(CompanyA, "Newer payee");
        newer.SysDateModified = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.ServiceProviders.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await new GetAllServiceProvidersHandler(db)
            .Handle(
                new GetAllServiceProvidersQuery
                {
                    Request = new DataGridRequest
                    {
                        SortBy = "SysDateModified",
                        SortDescending = true
                    }
                },
                CancellationToken.None);

        Assert.Equal(["Newer payee", "Older payee"], result.Data.Select(row => row.Name).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, "Doomed payee");
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();
        db.ServiceProviders.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllServiceProvidersHandler(db)
            .Handle(new GetAllServiceProvidersQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }
}
