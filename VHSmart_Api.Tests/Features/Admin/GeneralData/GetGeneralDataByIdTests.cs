using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class GetGeneralDataByIdTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Handle_ExistingRow_ReturnsRow()
    {
        var db = await CreateDbAsync(UserA());
        var row = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.PAYMENT,
            Category = "Payment Category",
            Name = "Invoice",
            Description = "Paid by invoice"
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();

        var response = await new GetGeneralDataByIdHandler(db)
            .Handle(new GetGeneralDataByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal(row.Id, response.Id);
        Assert.Equal(GeneralDataGroup.PAYMENT, response.Group);
        Assert.Equal("Payment Category", response.Category);
        Assert.Equal("Invoice", response.Name);
        Assert.Equal("Paid by invoice", response.Description);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetGeneralDataByIdHandler(db)
                .Handle(new GetGeneralDataByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var foreignRow = new GeneralDataEntity
        {
            CompanyId = Guid.NewGuid(),
            Group = GeneralDataGroup.COMPANY,
            Category = "Brand",
            Name = "Foreign brand"
        };
        db.GeneralData.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetGeneralDataByIdHandler(db)
                .Handle(new GetGeneralDataByIdQuery(foreignRow.Id), CancellationToken.None));
    }
}
