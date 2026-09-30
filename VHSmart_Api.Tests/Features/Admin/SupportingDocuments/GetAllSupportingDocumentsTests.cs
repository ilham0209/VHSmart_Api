using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class GetAllSupportingDocumentsTests
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

    private static SupportingDocumentEntity NewRow(
        Guid companyId,
        string documentType,
        SupportingDocumentForView forView = SupportingDocumentForView.RawMaterial,
        int sequence = 1,
        bool isMandatory = false,
        string? description = null) =>
        new()
        {
            CompanyId = companyId,
            ForView = forView,
            DocumentType = documentType,
            DocumentSequence = sequence,
            IsMandatory = isMandatory,
            Description = description
        };

    [Fact]
    public async Task Handle_SearchTerm_FindsDocumentTypeOrDescription()
    {
        var db = await CreateDbAsync(UserA());
        db.SupportingDocuments.AddRange(
            NewRow(CompanyA, "Halal Certificate"),
            NewRow(CompanyA, "Process Flow"),
            NewRow(CompanyA, "Ingredient Image", description: "raw material photo"));
        await db.SaveChangesAsync();

        var byDocumentType = await QueryAsync(db, "Certificate");
        var byDescription = await QueryAsync(db, "photo");

        Assert.Equal("Halal Certificate", Assert.Single(byDocumentType.Data).DocumentType);
        Assert.Equal("Ingredient Image", Assert.Single(byDescription.Data).DocumentType);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        db.SupportingDocuments.AddRange(
            NewRow(CompanyA, "Company A type"),
            NewRow(CompanyB, "Company B type"));
        await db.SaveChangesAsync();

        var asCompanyA = await new GetAllSupportingDocumentsHandler(db)
            .Handle(new GetAllSupportingDocumentsQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllSupportingDocumentsHandler(
                TestDbFactory.Create(databaseName, UserB()))
            .Handle(new GetAllSupportingDocumentsQuery(), CancellationToken.None);

        Assert.Equal("Company A type", Assert.Single(asCompanyA.Data).DocumentType);
        Assert.Equal("Company B type", Assert.Single(asCompanyB.Data).DocumentType);
    }

    [Fact]
    public async Task Handle_SortByDocumentSequence_Ascending()
    {
        var db = await CreateDbAsync(UserA());
        db.SupportingDocuments.AddRange(
            NewRow(CompanyA, "Fifteenth", sequence: 15),
            NewRow(CompanyA, "Second", sequence: 2),
            NewRow(CompanyA, "First", sequence: 1));
        await db.SaveChangesAsync();

        var result = await new GetAllSupportingDocumentsHandler(db)
            .Handle(
                new GetAllSupportingDocumentsQuery
                {
                    Request = new DataGridRequest { SortBy = "DocumentSequence" }
                },
                CancellationToken.None);

        Assert.Equal(
            ["First", "Second", "Fifteenth"],
            result.Data.Select(row => row.DocumentType).ToArray());
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var db = await CreateDbAsync(UserA());
        var row = NewRow(CompanyA, "Doomed type");
        db.SupportingDocuments.Add(row);
        await db.SaveChangesAsync();
        db.SupportingDocuments.Remove(row);
        await db.SaveChangesAsync();

        var result = await new GetAllSupportingDocumentsHandler(db)
            .Handle(new GetAllSupportingDocumentsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalRecords);
    }

    private static Task<DataGridResponse<GetAllSupportingDocumentsResponse>> QueryAsync(
        TestableVHSmartDbContext db,
        string searchTerm) =>
        new GetAllSupportingDocumentsHandler(db).Handle(
            new GetAllSupportingDocumentsQuery
            {
                Request = new DataGridRequest { SearchTerm = searchTerm }
            },
            CancellationToken.None);
}
