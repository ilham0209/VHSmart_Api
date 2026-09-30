using VHSmart_Api.Features.Admin.ServiceProviders;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.ServiceProviders;

public class GetServiceProviderByIdTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

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
            Description = "Payee row",
            Address = "Jalan Gombak 1",
            Postcode = "50450",
            CountryId = Guid.NewGuid(),
            State = "Wilayah Persekutuan",
            Telephone = "0380000000",
            Fax = "0380000001",
            Webpage = "https://provider.example",
            Email = "provider@example.com",
            ContactPerson = "Aminah",
            BankName = "CIMB",
            BankAccountNo = "8001234567"
        };

    [Fact]
    public async Task Handle_ExistingRow_ReturnsEveryFormField()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, "Santan Provider");
        db.ServiceProviders.Add(row);
        await db.SaveChangesAsync();

        var response = await new GetServiceProviderByIdHandler(db)
            .Handle(new GetServiceProviderByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal("Santan Provider", response.Name);
        Assert.Equal("Payee row", response.Description);
        Assert.Equal("Jalan Gombak 1", response.Address);
        Assert.Equal("50450", response.Postcode);
        Assert.Equal(row.CountryId, response.CountryId);
        Assert.Equal("Wilayah Persekutuan", response.State);
        Assert.Equal("0380000000", response.Telephone);
        Assert.Equal("0380000001", response.Fax);
        Assert.Equal("https://provider.example", response.Webpage);
        Assert.Equal("provider@example.com", response.Email);
        Assert.Equal("Aminah", response.ContactPerson);
        Assert.Equal("CIMB", response.BankName);
        Assert.Equal("8001234567", response.BankAccountNo);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetServiceProviderByIdHandler(db)
                .Handle(new GetServiceProviderByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B payee");
        db.ServiceProviders.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetServiceProviderByIdHandler(db)
                .Handle(new GetServiceProviderByIdQuery(foreignRow.Id), CancellationToken.None));
    }
}
