using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Personnel.Staff;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Personnel.Staff;

public class StaffPhotoTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartStaffPhotoTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public StaffPhotoTests()
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

    private static async Task<StaffEntity> SeedStaffAsync(
        TestableVHSmartDbContext db, Guid companyId)
    {
        var generalData = new List<GeneralDataEntity>
        {
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Title of Honour", Name = "Mr" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Department", Name = "Production" },
            new() { CompanyId = companyId, Group = GeneralDataGroup.PEOPLE, Category = "Designation", Name = "Halal Executive" }
        };
        db.GeneralData.AddRange(generalData);
        var row = new StaffEntity
        {
            CompanyId = companyId,
            Email = "staff@verify.my",
            TitleId = generalData[0].Id,
            Name = "Siti Aminah",
            DepartmentId = generalData[1].Id,
            DesignationId = generalData[2].Id
        };
        db.Staffs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static IFormFile PhotoFile(string fileName = "photo.png")
    {
        var bytes = (byte[])[137, 80, 78, 71]; // PNG magic bytes
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
        file.Headers = new HeaderDictionary
        {
            ["Content-Type"] = "image/png"
        };
        return file;
    }

    [Fact]
    public async Task Upload_ValidPhoto_StoresBytesAndReplacesThePreviousOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        var handler = new UploadStaffPhotoHandler(db, user, _storage, NullLogger<UploadStaffPhotoHandler>.Instance);

        await handler.Handle(
            new UploadStaffPhotoCommand(staff.Id, PhotoFile()), CancellationToken.None);
        var firstKey = (await db.Staffs.AsNoTracking().SingleAsync()).Photo!.StorageKey;

        var second = await handler.Handle(
            new UploadStaffPhotoCommand(staff.Id, PhotoFile("second.png")), CancellationToken.None);

        Assert.Equal("second.png", second.FileName);
        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Equal("second.png", stored.Photo!.FileName);
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Photo.StorageKey)));
        // The replaced file is gone from disk (A-03 pattern).
        Assert.False(File.Exists(Path.Combine(_storageRoot, firstKey)));
        Assert.Single(Directory.GetFiles(_storageRoot));
    }

    [Fact]
    public async Task Upload_DisallowedType_ThrowsBusinessRuleAndKeepsTheRowWithoutPhoto()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new UploadStaffPhotoHandler(db, user, _storage, NullLogger<UploadStaffPhotoHandler>.Instance)
                .Handle(new UploadStaffPhotoCommand(staff.Id, PhotoFile("policy.pdf")), CancellationToken.None));

        Assert.Contains("File type is not allowed", exception.Message);
        var stored = await db.Staffs.AsNoTracking().SingleAsync();
        Assert.Null(stored.Photo);
        var files = Directory.Exists(_storageRoot) ? Directory.GetFiles(_storageRoot) : Array.Empty<string>();
        Assert.Empty(files);
    }

    [Fact]
    public async Task Upload_UnknownStaff_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UploadStaffPhotoHandler(db, user, _storage, NullLogger<UploadStaffPhotoHandler>.Instance)
                .Handle(new UploadStaffPhotoCommand(Guid.NewGuid(), PhotoFile()), CancellationToken.None));
    }

    [Fact]
    public async Task Get_WithPhoto_StreamsTheStoredBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);
        await new UploadStaffPhotoHandler(db, user, _storage, NullLogger<UploadStaffPhotoHandler>.Instance)
            .Handle(new UploadStaffPhotoCommand(staff.Id, PhotoFile()), CancellationToken.None);

        var response = await new GetStaffPhotoHandler(db, user, _storage)
            .Handle(new GetStaffPhotoQuery(staff.Id), CancellationToken.None);

        Assert.Equal("image/png", response.ContentType);
        byte[] bytes;
        using (response.Content) // the FileStream must be closed before Dispose wipes the root
        {
            using var memory = new MemoryStream();
            await response.Content.CopyToAsync(memory);
            bytes = memory.ToArray();
        }

        Assert.Equal((byte[])[137, 80, 78, 71], bytes);
    }

    [Fact]
    public async Task Get_WithoutPhoto_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var staff = await SeedStaffAsync(db, CompanyA);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetStaffPhotoHandler(db, user, _storage)
                .Handle(new GetStaffPhotoQuery(staff.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Get_CrossCompanyRow_ThrowsNotFound()
    {
        var userA = UserA();
        var databaseName = TestDbFactory.NewDatabaseName();
        var db = TestDbFactory.Create(databaseName, userA);
        await db.Database.EnsureCreatedAsync();
        var foreign = await SeedStaffAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetStaffPhotoHandler(db, userA, _storage)
                .Handle(new GetStaffPhotoQuery(foreign.Id), CancellationToken.None));
    }
}
