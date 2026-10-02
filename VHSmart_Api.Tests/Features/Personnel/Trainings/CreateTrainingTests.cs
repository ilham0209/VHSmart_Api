using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class CreateTrainingTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresTrainingAndAttendees()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        await TrainingTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", companyId);
        var siti = await TrainingTestData.SeedStaffAsync(db, companyId, "Siti Aminah");
        var abu = await TrainingTestData.SeedStaffAsync(db, companyId, "Abu Bakar");
        var command = TrainingTestData.ValidCreateCommand(
            TrainingType.InternalHalalCommittee,
            "IHC Briefing",
            new DateTime(2026, 5, 5),
            attendees: [siti, abu]);

        var response = await new CreateTrainingHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal("IHC Briefing", response.Name);
        Assert.Equal(TrainingType.InternalHalalCommittee, response.TrainingType);
        Assert.Equal(new DateTime(2026, 5, 5), response.TrainingDate);
        Assert.Equal(user.UserId, (await db.Trainings.SingleAsync()).SysUserCreated);
        Assert.Equal(2, await db.TrainingAttendees.CountAsync());
        Assert.Equal(
            new[] { "Abu Bakar", "Siti Aminah" },
            response.Attendees.Select(row => row.Name));
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        await TrainingTestData.SeedTrainingAsync(db, user.CompanyId, "Halal Awareness 101");

        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateTrainingHandler(db, user)
                .Handle(TrainingTestData.ValidCreateCommand(name: "Halal Awareness 101"),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameNameInAnotherCompany_IsAllowed()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var ownCompany = user.CompanyId;
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        await TrainingTestData.SeedTrainingAsync(db, foreignCompany, "Shared Course Name");
        await TrainingTestData.SeedStaffAsync(db, ownCompany);

        var response = await new CreateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidCreateCommand(name: "Shared Course Name"),
                CancellationToken.None);

        Assert.Equal("Shared Course Name", response.Name);
    }

    [Fact]
    public async Task Handle_RepeatedAttendeeId_StoresOneRow()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var staffId = await TrainingTestData.SeedStaffAsync(db, companyId);

        var response = await new CreateTrainingHandler(db, user)
            .Handle(TrainingTestData.ValidCreateCommand(attendees: [staffId, staffId]),
                CancellationToken.None);

        Assert.Single(response.Attendees);
        Assert.Equal(1, await db.TrainingAttendees.CountAsync());
    }

    [Fact]
    public async Task Validator_MissingTrainingType_FailsWithRequired()
    {
        var db = await TrainingTestData.CreateDbAsync(TrainingTestData.CompanyUser());
        var validator = new CreateTrainingValidator(db, TrainingTestData.CompanyUser());

        var result = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(trainingType: null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors, failure => failure.ErrorMessage == "Training type is required.");
    }

    [Fact]
    public async Task Validator_OutOfRangeTrainingType_FailsWithInvalid()
    {
        var db = await TrainingTestData.CreateDbAsync(TrainingTestData.CompanyUser());
        var validator = new CreateTrainingValidator(db, TrainingTestData.CompanyUser());

        var result = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(trainingType: (TrainingType)99));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors, failure => failure.ErrorMessage == "Training type is invalid.");
    }

    [Fact]
    public async Task Validator_MissingNameDateOrAttendance_Fails()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var validator = new CreateTrainingValidator(db, user);

        var noName = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(name: " "));
        var noDate = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(date: default(DateTime)));
        var noAttendees = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(attendees: []));

        Assert.False(noName.IsValid);
        Assert.Contains(
            noName.Errors, failure => failure.ErrorMessage == "Training name is required.");
        Assert.False(noDate.IsValid);
        Assert.Contains(
            noDate.Errors, failure => failure.ErrorMessage == "Training date is required.");
        Assert.False(noAttendees.IsValid);
        Assert.Contains(
            noAttendees.Errors, failure => failure.ErrorMessage == "Attendance is required.");
    }

    [Fact]
    public async Task Validator_NameLongerThan200_Fails()
    {
        var db = await TrainingTestData.CreateDbAsync(TrainingTestData.CompanyUser());
        var validator = new CreateTrainingValidator(db, TrainingTestData.CompanyUser());

        var result = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(name: new string('x', 201)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Training name must be 200 characters or fewer.");
    }

    [Fact]
    public async Task Validator_UnknownOrForeignAttendee_Fails()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        await TrainingTestData.SeedCompanyAsync(db, "Own company", user.CompanyId);
        var foreignCompany = await TrainingTestData.SeedCompanyAsync(db, "Foreign company");
        var foreignStaff = await TrainingTestData.SeedStaffAsync(db, foreignCompany);
        var validator = new CreateTrainingValidator(db, user);

        var unknown = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(attendees: [Guid.NewGuid()]));
        var foreign = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(attendees: [foreignStaff]));

        Assert.False(unknown.IsValid);
        Assert.Contains(
            unknown.Errors, failure => failure.ErrorMessage == "Attendee not found.");
        Assert.False(foreign.IsValid);
        Assert.Contains(
            foreign.Errors, failure => failure.ErrorMessage == "Attendee not found.");
    }

    [Fact]
    public async Task Validator_ValidStaff_Passes()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var companyId = user.CompanyId;
        var staffId = await TrainingTestData.SeedStaffAsync(db, companyId);
        var validator = new CreateTrainingValidator(db, user);

        var result = await validator.ValidateAsync(
            TrainingTestData.ValidCreateCommand(attendees: [staffId]));

        Assert.True(result.IsValid);
    }
}
