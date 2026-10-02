using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class UpdateTrainingTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesFields()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var staffId = await TrainingTestData.SeedStaffAsync(db, companyId);
        var trainingId = await TrainingTestData.SeedTrainingAsync(
            db, companyId, "Old name", attendees: [staffId]);

        var response = await new UpdateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidUpdateCommand(
                trainingId,
                TrainingType.SlaughtermanHalalChecker,
                "New name",
                new DateTime(2026, 7, 1),
                [staffId]),
                CancellationToken.None);

        Assert.Equal("New name", response.Name);
        Assert.Equal(TrainingType.SlaughtermanHalalChecker, response.TrainingType);
        Assert.Equal(new DateTime(2026, 7, 1), response.TrainingDate);
        var stored = await db.Trainings.AsNoTracking().SingleAsync();
        Assert.Equal("New name", stored.Name);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_DifferentAttendance_ReconcilesWithoutDuplicates()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var stay = await TrainingTestData.SeedStaffAsync(db, companyId, "Stays Person");
        var leave = await TrainingTestData.SeedStaffAsync(db, companyId, "Leaves Person");
        var join = await TrainingTestData.SeedStaffAsync(db, companyId, "Joins Person");
        var trainingId = await TrainingTestData.SeedTrainingAsync(
            db, companyId, attendees: [stay, leave]);

        var response = await new UpdateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidUpdateCommand(
                trainingId, attendees: [stay, join]),
                CancellationToken.None);

        Assert.Equal(
            new[] { "Joins Person", "Stays Person" },
            response.Attendees.Select(row => row.Name));

        var all = await db.TrainingAttendees.IgnoreQueryFilters()
            .Where(row => row.TrainingId == trainingId)
            .ToListAsync();
        Assert.Equal(3, all.Count);
        Assert.Equal(2, all.Count(row => !row.IsDeleted));
        var removed = Assert.Single(all, row => row.IsDeleted);
        Assert.Equal(leave, removed.StaffId);
        Assert.Equal(user.UserId, removed.SysUserModified);
    }

    [Fact]
    public async Task Handle_SameAttendance_KeepsRowsUntouched()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var staffId = await TrainingTestData.SeedStaffAsync(db, companyId);
        var trainingId = await TrainingTestData.SeedTrainingAsync(
            db, companyId, attendees: [staffId]);
        var attendeeId = (await db.TrainingAttendees.SingleAsync()).Id;

        await new UpdateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidUpdateCommand(trainingId, attendees: [staffId]),
                CancellationToken.None);

        var row = await db.TrainingAttendees.SingleAsync();
        Assert.Equal(attendeeId, row.Id);
        Assert.False(row.IsDeleted);
        Assert.Equal(1, await db.TrainingAttendees.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Handle_ClearingAttendanceAll_ThrowsValidator()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, companyId);
        var validator = new UpdateTrainingValidator(db, user);

        var result = await validator.ValidateAsync(
            TrainingTestData.ValidUpdateCommand(trainingId, attendees: []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors, failure => failure.ErrorMessage == "Attendance is required.");
    }

    [Fact]
    public async Task Handle_DuplicateName_ExcludingSelf_ThrowsConflict()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        await TrainingTestData.SeedTrainingAsync(db, companyId, "Taken Name");
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, companyId, "My Name");

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateTrainingHandler(db, user)
                .Handle(TrainingTestData.ValidUpdateCommand(trainingId, name: "Taken Name"),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatingWithOwnName_IsAllowed()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var trainingId = await TrainingTestData.SeedTrainingAsync(db, companyId, "My Name");

        var response = await new UpdateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidUpdateCommand(trainingId, name: "My Name"),
                CancellationToken.None);

        Assert.Equal("My Name", response.Name);
    }

    [Fact]
    public async Task Handle_UnknownTraining_ThrowsNotFound()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateTrainingHandler(db, user)
                .Handle(TrainingTestData.ValidUpdateCommand(Guid.NewGuid()),
                    CancellationToken.None));
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
            new UpdateTrainingHandler(db, user)
                .Handle(TrainingTestData.ValidUpdateCommand(foreignTraining),
                    CancellationToken.None));

        Assert.Equal(0, await db.TrainingAttendees.IgnoreQueryFilters()
            .CountAsync(row => row.TrainingId == foreignTraining));
    }
}
