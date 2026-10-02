using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.HalalPolicy;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.CompanyInformation.HalalPolicy;

public class GetHalalPolicyDocumentTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartHalalPolicyDocumentTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public GetHalalPolicyDocumentTests()
    {
        _storage = new LocalFileStorage(_storageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<HalalPolicyEntity> SeedRowAsync(TestableVHSmartDbContext db, Guid companyId)
    {
        var document = await _storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "policy.pdf", "application/pdf");
        var row = new HalalPolicyEntity
        {
            CompanyId = companyId,
            SchemeId = await db.Schemes.Select(scheme => scheme.Id).FirstAsync(),
            PolicyDate = new DateTime(2026, 1, 15),
            Document = document
        };
        db.HalalPolicies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Handle_RowWithDocument_StreamsTheStoredBytes()
    {
        var user = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA);
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, CompanyA);

        var response = await new GetHalalPolicyDocumentHandler(db, user, _storage)
            .Handle(new GetHalalPolicyDocumentQuery(row.Id), CancellationToken.None);

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
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA);
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetHalalPolicyDocumentHandler(db, user, _storage)
                .Handle(new GetHalalPolicyDocumentQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingBytesOnDisk_ThrowsNotFound()
    {
        var user = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA);
        var db = await CreateDbAsync(user);
        var row = await SeedRowAsync(db, CompanyA);
        File.Delete(Path.Combine(_storageRoot, row.Document.StorageKey));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetHalalPolicyDocumentHandler(db, user, _storage)
                .Handle(new GetHalalPolicyDocumentQuery(row.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CrossCompanyRow_ThrowsNotFound()
    {
        var userA = new TestCurrentUser(Guid.NewGuid().ToString(), CompanyA);
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreignRow = await SeedRowAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetHalalPolicyDocumentHandler(db, userA, _storage)
                .Handle(new GetHalalPolicyDocumentQuery(foreignRow.Id), CancellationToken.None));
    }
}
