using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.SupportingDocuments;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Admin.SupportingDocuments;

public class GetSupportingDocumentTemplateTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartSupportingDocTemplateTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public GetSupportingDocumentTemplateTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static SupportingDocumentEntity NewRow(Guid companyId, StoredFile? template = null) =>
        new()
        {
            CompanyId = companyId,
            ForView = SupportingDocumentForView.SopHas,
            DocumentType = "HAS Checklist",
            DocumentSequence = 8,
            IsMandatory = false,
            Description = null,
            Template = template
        };

    [Fact]
    public async Task Handle_RowWithTemplate_StreamsTheStoredBytes()
    {
        var db = await CreateDbAsync(UserA());
        var stored = await _storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "has.pdf", "application/pdf"); // %PDF
        db.SupportingDocuments.Add(NewRow(CompanyA, stored));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();

        var response = await new GetSupportingDocumentTemplateHandler(db, _storage)
            .Handle(new GetSupportingDocumentTemplateQuery(row.Id), CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        byte[] bytes;
        using (response.Content) // the FileStream must be closed before Dispose wipes the root
        {
            using var memory = new MemoryStream();
            await response.Content.CopyToAsync(memory);
            bytes = memory.ToArray();
        }

        Assert.Equal((byte[])[37, 80, 68, 70], bytes);
    }

    [Fact]
    public async Task Handle_RowWithoutTemplate_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        db.SupportingDocuments.Add(NewRow(CompanyA));
        await db.SaveChangesAsync();
        var row = await db.SupportingDocuments.SingleAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSupportingDocumentTemplateHandler(db, _storage)
                .Handle(new GetSupportingDocumentTemplateQuery(row.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSupportingDocumentTemplateHandler(db, _storage)
                .Handle(new GetSupportingDocumentTemplateQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, UserA());
        await db.Database.EnsureCreatedAsync();
        var foreignRow = NewRow(CompanyB);
        db.SupportingDocuments.Add(foreignRow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSupportingDocumentTemplateHandler(db, _storage)
                .Handle(new GetSupportingDocumentTemplateQuery(foreignRow.Id), CancellationToken.None));
    }
}
