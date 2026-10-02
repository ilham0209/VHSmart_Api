using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class TrainingModuleTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "VHSmartTrainingModuleTests", $"storage-{Guid.NewGuid():N}");

    private readonly LocalFileStorage _storage;

    public TrainingModuleTests() => _storage = new LocalFileStorage(_storageRoot);

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

    private static IFormFile ModuleFile(string fileName = "module.pdf") =>
        new FormFile(new MemoryStream([37, 80, 68, 70]), 0, 4, "File", fileName);

    [Fact]
    public async Task Add_ValidFile_StoresRowAndBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var command = new AddTrainingModuleCommand(
            trainingId, moduleTypeId, "Slide 1", ModuleFile());

        var response = await AddHandler(db, user).Handle(command, CancellationToken.None);

        var module = Assert.Single(response.Modules);
        Assert.Equal("Slide 1", module.ModuleName);
        Assert.Equal("Presentation", module.ModuleType);
        Assert.Equal("module.pdf", module.FileName);
        var stored = await db.TrainingModules.AsNoTracking().SingleAsync();
        Assert.True(File.Exists(Path.Combine(_storageRoot, stored.Document!.StorageKey)));
        Assert.Equal(user.UserId, stored.SysUserCreated);
    }

    [Fact]
    public async Task Add_WithoutFile_StoresModuleWithoutBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);

        var response = await AddHandler(db, user).Handle(
            new AddTrainingModuleCommand(trainingId, moduleTypeId, "Notes", null),
            CancellationToken.None);

        var module = Assert.Single(response.Modules);
        Assert.Null(module.FileName);
        var stored = await db.TrainingModules.AsNoTracking().SingleAsync();
        Assert.Null(stored.Document);
    }

    [Fact]
    public async Task Add_DisallowedFileType_ThrowsBusinessRuleAndStoresNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var command = new AddTrainingModuleCommand(
            trainingId, moduleTypeId, "Slide 1", ModuleFile("payload.exe"));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            AddHandler(db, user).Handle(command, CancellationToken.None));

        Assert.Equal(0, await db.TrainingModules.CountAsync());
    }

    [Fact]
    public async Task Add_UnknownOrForeignTraining_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignTraining = await TrainingTestData.SeedTrainingAsync(db, foreignCompany);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AddHandler(db, user).Handle(
                new AddTrainingModuleCommand(Guid.NewGuid(), moduleTypeId, "Slide 1", null),
                CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AddHandler(db, user).Handle(
                new AddTrainingModuleCommand(foreignTraining, moduleTypeId, "Slide 1", null),
                CancellationToken.None));

        Assert.Equal(0, await db.TrainingModules.CountAsync());
    }

    [Fact]
    public async Task Validator_UnknownOrForeignModuleType_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        // Creates the PEOPLE values, including a Designation row of the caller's company.
        await TrainingTestData.SeedStaffAsync(db, CompanyA);
        var validator = new AddTrainingModuleValidator(db, user);

        // A PEOPLE row of the caller's company is not a TRAINING / Module Type row.
        var wrongCategory = await db.GeneralData
            .Where(row => row.CompanyId == CompanyA && row.Category == "Designation")
            .Select(row => row.Id)
            .FirstAsync();
        var unknown = await validator.ValidateAsync(new AddTrainingModuleCommand(
            trainingId, Guid.NewGuid(), "Slide 1", null));
        var wrong = await validator.ValidateAsync(new AddTrainingModuleCommand(
            trainingId, wrongCategory, "Slide 1", null));

        Assert.False(unknown.IsValid);
        Assert.Contains(
            unknown.Errors, failure => failure.ErrorMessage == "Module type not found.");
        Assert.False(wrong.IsValid);
        Assert.Contains(
            wrong.Errors, failure => failure.ErrorMessage == "Module type not found.");
    }

    [Fact]
    public async Task Validator_MissingModuleName_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var validator = new AddTrainingModuleValidator(db, user);

        var result = await validator.ValidateAsync(new AddTrainingModuleCommand(
            trainingId, moduleTypeId, " ", null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors, failure => failure.ErrorMessage == "Module name is required.");
    }

    [Fact]
    public async Task Delete_SoftDeletesTheModule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var moduleId = await SeedModuleAsync(db, CompanyA, trainingId, moduleTypeId);

        await new DeleteTrainingModuleHandler(db, user)
            .Handle(new DeleteTrainingModuleCommand(trainingId, moduleId),
                CancellationToken.None);

        Assert.Equal(0, await db.TrainingModules.CountAsync());
        Assert.True(await db.TrainingModules.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == moduleId && row.IsDeleted));
    }

    [Fact]
    public async Task Delete_UnknownOrWrongTrainingOrForeign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var otherTrainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA, "Other");
        var moduleId = await SeedModuleAsync(db, CompanyA, trainingId, moduleTypeId);
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignTraining = await TrainingTestData.SeedTrainingAsync(db, foreignCompany);
        var foreignModuleId = await SeedModuleAsync(db, foreignCompany, foreignTraining, moduleTypeId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteTrainingModuleHandler(db, user)
                .Handle(new DeleteTrainingModuleCommand(trainingId, Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteTrainingModuleHandler(db, user)
                .Handle(new DeleteTrainingModuleCommand(otherTrainingId, moduleId),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteTrainingModuleHandler(db, user)
                .Handle(new DeleteTrainingModuleCommand(foreignTraining, foreignModuleId),
                    CancellationToken.None));

        Assert.True(await db.TrainingModules.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == moduleId && !row.IsDeleted));
    }

    [Fact]
    public async Task Document_StreamsTheStoredBytes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        var document = await _storage.SaveAsync(
            new MemoryStream([37, 80, 68, 70]), "module.pdf", "application/pdf");
        var moduleId = await SeedModuleAsync(db, CompanyA, trainingId, moduleTypeId, document);

        var response = await new GetTrainingModuleDocumentHandler(db, user, _storage)
            .Handle(new GetTrainingModuleDocumentQuery(trainingId, moduleId),
                CancellationToken.None);

        Assert.Equal("application/pdf", response.ContentType);
        using var reader = new StreamReader(response.Content);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Document_WithoutBytesOrForeignOrUnknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, CompanyA);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, CompanyA);
        db.TrainingModules.Add(new TrainingModuleEntity
        {
            CompanyId = CompanyA,
            TrainingId = trainingId,
            ModuleTypeId = moduleTypeId,
            ModuleName = "No file yet"
        });
        await db.SaveChangesAsync();
        var filelessId = (await db.TrainingModules.SingleAsync()).Id;
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignTraining = await TrainingTestData.SeedTrainingAsync(db, foreignCompany);
        var foreignModuleId = await SeedModuleAsync(db, foreignCompany, foreignTraining, moduleTypeId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTrainingModuleDocumentHandler(db, user, _storage)
                .Handle(new GetTrainingModuleDocumentQuery(trainingId, filelessId),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTrainingModuleDocumentHandler(db, user, _storage)
                .Handle(new GetTrainingModuleDocumentQuery(trainingId, Guid.NewGuid()),
                    CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTrainingModuleDocumentHandler(db, user, _storage)
                .Handle(new GetTrainingModuleDocumentQuery(foreignTraining, foreignModuleId),
                    CancellationToken.None));
    }

    [Fact]
    public async Task StaffOptions_OnlyOwnCompanyStaffWithDesignation()
    {
        var companyId = Guid.NewGuid();
        var user = TrainingTestData.CompanyUser(companyId);
        var db = await CreateDbAsync(user);
        await TrainingTestData.SeedStaffAsync(db, companyId, "Siti Aminah");
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        await TrainingTestData.SeedStaffAsync(db, foreignCompany, "Foreign Person");

        var options = await new GetTrainingStaffOptionsHandler(db, user)
            .Handle(new GetTrainingStaffOptionsQuery(), CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal("Siti Aminah", option.Name);
        Assert.Equal("Halal Executive", option.Designation);
    }

    private AddTrainingModuleHandler AddHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) =>
        new(db, user, _storage, NullLogger<AddTrainingModuleHandler>.Instance);

    private static async Task<Guid> SeedModuleAsync(
        TestableVHSmartDbContext db,
        Guid companyId,
        Guid trainingId,
        Guid moduleTypeId,
        StoredFile? document = null)
    {
        var row = new TrainingModuleEntity
        {
            CompanyId = companyId,
            TrainingId = trainingId,
            ModuleTypeId = moduleTypeId,
            ModuleName = "Seeded module",
            Document = document ?? new StoredFile
            {
                FileName = "seeded.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf",
                SizeBytes = 4
            }
        };
        db.TrainingModules.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }
}
