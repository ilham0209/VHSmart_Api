using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class GetTrainingByIdTests
{
    [Fact]
    public async Task Handle_ReturnsTrainingAttendeesAndModules()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        await TrainingTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", companyId);
        var siti = await TrainingTestData.SeedStaffAsync(
            db, companyId, "Siti Aminah", ihcMember: true, designation: "Halal Executive");
        var abu = await TrainingTestData.SeedStaffAsync(
            db, companyId, "Abu Bakar", designation: "Slaughterman");
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, companyId);
        var trainingId = await TrainingTestData.SeedTrainingAsync(
            db, companyId,
            name: "Halal Awareness 101",
            date: new DateTime(2026, 3, 15),
            trainingType: TrainingType.SlaughtermanHalalChecker,
            attendees: [siti, abu]);
        db.TrainingModules.Add(new TrainingModuleEntity
        {
            CompanyId = companyId,
            TrainingId = trainingId,
            ModuleTypeId = moduleTypeId,
            ModuleName = "Slide 1",
            Document = new StoredFile
            {
                FileName = "slide-1.pdf",
                StorageKey = Guid.NewGuid().ToString("D"),
                ContentType = "application/pdf",
                SizeBytes = 4
            }
        });
        await db.SaveChangesAsync();

        var detail = await new GetTrainingByIdHandler(db, user)
            .Handle(new GetTrainingByIdQuery(trainingId), CancellationToken.None);

        Assert.Equal("Halal Awareness 101", detail.Name);
        Assert.Equal(TrainingType.SlaughtermanHalalChecker, detail.TrainingType);
        Assert.Equal("Verify Halal Sdn Bhd", detail.Company);
        Assert.Equal(new DateTime(2026, 3, 15), detail.TrainingDate);

        Assert.Equal(2, detail.Attendees.Count);
        // Ordered by name, numbered from 1 (spec 7.5 attendance table).
        Assert.Equal(new[] { "Abu Bakar", "Siti Aminah" }, detail.Attendees.Select(row => row.Name));
        Assert.Equal(new[] { 1, 2 }, detail.Attendees.Select(row => row.No));
        Assert.Equal("Slaughterman", detail.Attendees[0].Designation);
        Assert.False(detail.Attendees[0].IsIhcMember);
        Assert.True(detail.Attendees[1].IsIhcMember);
        Assert.Equal("Member", detail.Attendees[1].IhcRole);

        var module = Assert.Single(detail.Modules);
        Assert.Equal(1, module.No);
        Assert.Equal("Slide 1", module.ModuleName);
        Assert.Equal("Presentation", module.ModuleType);
        Assert.Equal("slide-1.pdf", module.FileName);
    }

    [Fact]
    public async Task Handle_UnknownTraining_ThrowsNotFound()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTrainingByIdHandler(db, user)
                .Handle(new GetTrainingByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesTraining_ThrowsNotFound()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        await TrainingTestData.SeedCompanyAsync(db, "Own company");
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignTraining = await TrainingTestData.SeedTrainingAsync(db, foreignCompany);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetTrainingByIdHandler(db, user)
                .Handle(new GetTrainingByIdQuery(foreignTraining), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedAttendeeOrModule_IsNotShown()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var staffId = await TrainingTestData.SeedStaffAsync(db, companyId);
        var moduleTypeId = await TrainingTestData.SeedModuleTypeAsync(db, companyId);
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, companyId, attendees: [staffId]);
        db.TrainingModules.Add(new TrainingModuleEntity
        {
            CompanyId = companyId,
            TrainingId = trainingId,
            ModuleTypeId = moduleTypeId,
            ModuleName = "Removed module"
        });
        await db.SaveChangesAsync();

        var module = await db.TrainingModules.SingleAsync(row => row.TrainingId == trainingId);
        db.TrainingModules.Remove(module);
        await db.SaveChangesAsync();

        var detail = await new GetTrainingByIdHandler(db, user)
            .Handle(new GetTrainingByIdQuery(trainingId), CancellationToken.None);

        Assert.Single(detail.Attendees);
        Assert.Empty(detail.Modules);
    }
}
