using VHSmart_Api.Features.Admin.WebLinks;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.WebLinks;

public class GetWebLinkByIdTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static WebLinkEntity NewRow(Guid companyId, string name) =>
        new()
        {
            CompanyId = companyId,
            Name = name,
            Webpage = "https://verifyhalal.com",
            Description = "Halal verification",
            Icon = new()
            {
                FileName = "verify.png",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "image/png"
            }
        };

    [Fact]
    public async Task Handle_ExistingRow_ReturnsFormFieldsAndIconFileName()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        var row = NewRow(CompanyA, "Verify Halal");
        db.WebLinks.Add(row);
        await db.SaveChangesAsync();

        var response = await new GetWebLinkByIdHandler(db)
            .Handle(new GetWebLinkByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal("Verify Halal", response.Name);
        Assert.Equal("https://verifyhalal.com", response.Webpage);
        Assert.Equal("Halal verification", response.Description);
        Assert.Equal("verify.png", response.IconFileName);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWebLinkByIdHandler(db)
                .Handle(new GetWebLinkByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B link");
        db.WebLinks.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWebLinkByIdHandler(db)
                .Handle(new GetWebLinkByIdQuery(foreignRow.Id), CancellationToken.None));
    }
}
