using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class DeleteSupportingDocumentTests
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
        SupportingDocumentForView forView = SupportingDocumentForView.HalalApplication,
        int sequence = 1,
        bool withTemplate = false) =>
        new()
        {
            CompanyId = companyId,
            ForView = forView,
            DocumentType = documentType,
            DocumentSequence = sequence,
            IsMandatory = false,
            Description = null,
            Template = withTemplate
                ? new()
                {
                    FileName = "template.pdf",
                    StorageKey = Guid.NewGuid().ToString("D"),
                    ContentType = "application/pdf"
                }
                : null
        };

    [Fact]
    public async Task Handle_ExistingRow_SoftDeletesAndHidesIt()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        db.SupportingDocuments.Add(NewRow(CompanyA, "Halal Certificate"));
        await db.SaveChangesAsync();

        var row = await db.SupportingDocuments.SingleAsync();
        await new DeleteSupportingDocumentHandler(db)
            .Handle(new DeleteSupportingDocumentCommand(row.Id), CancellationToken.None);

        // CodingRules 7.1: the row stays, the flag flips; the global filter hides it.
        var stored = await db.SupportingDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Empty(await db.SupportingDocuments.ToArrayAsync());
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteSupportingDocumentHandler(db)
                .Handle(new DeleteSupportingDocumentCommand(Guid.NewGuid()), CancellationToken.None));
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
            new DeleteSupportingDocumentHandler(db)
                .Handle(new DeleteSupportingDocumentCommand(foreignRow.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeletedRow_ThrowsNotFound()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        db.SupportingDocuments.Add(NewRow(CompanyA, "Halal Certificate"));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();

        await new DeleteSupportingDocumentHandler(db)
            .Handle(new DeleteSupportingDocumentCommand(row.Id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteSupportingDocumentHandler(db)
                .Handle(new DeleteSupportingDocumentCommand(row.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_KeepsTheTemplateMetadataOnTheSoftDeletedRow()
    {
        var db = await CreateDbAsync(new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA));
        db.SupportingDocuments.Add(NewRow(CompanyA, "SOP Checklist", withTemplate: true));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();
        var storageKey = row.Template!.StorageKey;

        await new DeleteSupportingDocumentHandler(db)
            .Handle(new DeleteSupportingDocumentCommand(row.Id), CancellationToken.None);

        // The bytes stay with the row, exactly like the CB logo and the web-link icon.
        var stored = await db.SupportingDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(storageKey, stored.Template!.StorageKey);
        Assert.Equal("template.pdf", stored.Template.FileName);
    }
}
