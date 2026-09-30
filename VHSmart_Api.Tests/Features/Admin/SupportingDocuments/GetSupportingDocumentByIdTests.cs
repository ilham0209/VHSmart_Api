using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class GetSupportingDocumentByIdTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static SupportingDocumentEntity NewRow(
        Guid companyId,
        string documentType,
        bool withTemplate = false) =>
        new()
        {
            CompanyId = companyId,
            ForView = SupportingDocumentForView.SopDocumentsAndRecords,
            DocumentType = documentType,
            DocumentSequence = 2,
            IsMandatory = true,
            Description = "Documents and records",
            Template = withTemplate
                ? new()
                {
                    FileName = "sop-dr.pdf",
                    StorageKey = Guid.NewGuid().ToString("D"),
                    ContentType = "application/pdf"
                }
                : null
        };

    [Fact]
    public async Task Handle_ExistingRow_ReturnsEveryFormField()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        db.SupportingDocuments.Add(NewRow(CompanyA, "Records Control"));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();

        var response = await new GetSupportingDocumentByIdHandler(db)
            .Handle(new GetSupportingDocumentByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal(SupportingDocumentForView.SopDocumentsAndRecords, response.ForView);
        Assert.Equal("Records Control", response.DocumentType);
        Assert.Equal(2, response.DocumentSequence);
        Assert.True(response.IsMandatory);
        Assert.Equal("Documents and records", response.Description);
        // No template on this row - the bytes endpoint 404s until one is uploaded.
        Assert.Null(response.TemplateFileName);
    }

    [Fact]
    public async Task Handle_RowWithTemplate_ReturnsTheFileName()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        db.SupportingDocuments.Add(NewRow(CompanyA, "Records Control", withTemplate: true));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();

        var response = await new GetSupportingDocumentByIdHandler(db)
            .Handle(new GetSupportingDocumentByIdQuery(row.Id), CancellationToken.None);

        Assert.Equal("sop-dr.pdf", response.TemplateFileName);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSupportingDocumentByIdHandler(db)
                .Handle(new GetSupportingDocumentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB, "Company B type");
        db.SupportingDocuments.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSupportingDocumentByIdHandler(db)
                .Handle(new GetSupportingDocumentByIdQuery(foreignRow.Id), CancellationToken.None));
    }
}
