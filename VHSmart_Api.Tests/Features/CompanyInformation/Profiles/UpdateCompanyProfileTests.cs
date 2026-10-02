using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

public class UpdateCompanyProfileTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresBothPeopleHoursAndHeadcount()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var contactId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId, "Aiman Rahman");
        var executiveId = await ProfileTestData.SeedStaffAsync(
            db, user.CompanyId, "Nurul Islam", "Manager");
        var command = ProfileTestData.ValidCommand(
            user.CompanyId, contactId, executiveId, numberOfEmployees: 42);

        var response = await new UpdateCompanyProfileHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(42, response.NumberOfEmployees);
        Assert.Equal(contactId, response.ContactPerson?.StaffId);
        Assert.Equal("Aiman Rahman", response.ContactPerson?.Name);
        Assert.Equal("Halal Executive", response.ContactPerson?.Designation);
        Assert.Equal(new TimeOnly(9, 0), response.ContactPerson?.WorkingHourFrom);
        Assert.Equal(new TimeOnly(17, 30), response.ContactPerson?.WorkingHourTo);
        Assert.Equal(executiveId, response.HalalExecutive?.StaffId);
        Assert.Equal(new TimeOnly(8, 0), response.HalalExecutive?.WorkingHourFrom);

        var rows = await db.CompanyContacts.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(user.UserId, rows[0].SysUserCreated);
        Assert.All(rows, row => Assert.NotEqual(default, row.SysDateCreated));
    }

    [Fact]
    public async Task Handle_NullStaffIds_ClearsTheSlotsAndTheHeadcount()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var contactId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId);
        await ProfileTestData.SeedContactAsync(
            db, user.CompanyId, CompanyContactKind.ContactPerson, contactId);
        var company = await db.Companies.SingleAsync(row => row.Id == user.CompanyId);
        company.NumberOfEmployees = 42;
        await db.SaveChangesAsync();

        var response = await new UpdateCompanyProfileHandler(db, user)
            .Handle(ProfileTestData.ValidCommand(user.CompanyId, numberOfEmployees: null),
                CancellationToken.None);

        Assert.Null(response.ContactPerson);
        Assert.Null(response.HalalExecutive);
        Assert.Null(response.NumberOfEmployees);
        Assert.Equal(0, await db.CompanyContacts.CountAsync(
            row => row.CompanyId == user.CompanyId));
        Assert.True(await db.CompanyContacts.IgnoreQueryFilters().AnyAsync(
            row => row.CompanyId == user.CompanyId && row.IsDeleted));
        Assert.Null(await db.Companies.AsNoTracking()
            .Where(row => row.Id == user.CompanyId)
            .Select(row => row.NumberOfEmployees)
            .SingleAsync());
    }

    [Fact]
    public async Task Handle_DifferentStaff_ReplacesTheRowWithoutDuplicates()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var firstId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId, "First Person");
        var secondId = await ProfileTestData.SeedStaffAsync(
            db, user.CompanyId, "Second Person", "Manager");
        await ProfileTestData.SeedContactAsync(
            db, user.CompanyId, CompanyContactKind.ContactPerson, firstId);

        await new UpdateCompanyProfileHandler(db, user)
            .Handle(ProfileTestData.ValidCommand(
                    user.CompanyId, contactPersonStaffId: secondId, numberOfEmployees: null),
                CancellationToken.None);

        var rows = await db.CompanyContacts.AsNoTracking()
            .Where(row => row.CompanyId == user.CompanyId && row.Kind == CompanyContactKind.ContactPerson)
            .ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(secondId, row.StaffId);
        Assert.False(row.IsDeleted);
        var all = await db.CompanyContacts.IgnoreQueryFilters()
            .Where(r => r.CompanyId == user.CompanyId)
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Single(all, r => !r.IsDeleted && r.StaffId == secondId);
        Assert.Single(all, r => r.IsDeleted && r.StaffId == firstId);
    }

    [Fact]
    public async Task Handle_SameStaff_UpdatesTheWorkingHoursInPlace()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var contactId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId);
        await ProfileTestData.SeedContactAsync(
            db, user.CompanyId, CompanyContactKind.ContactPerson, contactId,
            new TimeOnly(9, 0), new TimeOnly(17, 0));
        var existingId = (await db.CompanyContacts.SingleAsync(
            row => row.Kind == CompanyContactKind.ContactPerson)).Id;

        var response = await new UpdateCompanyProfileHandler(db, user)
            .Handle(ProfileTestData.ValidCommand(
                    user.CompanyId, contactPersonStaffId: contactId, numberOfEmployees: null)
                with
            {
                ContactPersonWorkingHourFrom = new TimeOnly(10, 0),
                ContactPersonWorkingHourTo = new TimeOnly(18, 0)
            },
                CancellationToken.None);

        var row = Assert.Single(await db.CompanyContacts.AsNoTracking()
            .Where(r => r.CompanyId == user.CompanyId)
            .ToListAsync());
        Assert.Equal(existingId, row.Id);
        Assert.Equal(new TimeOnly(10, 0), row.WorkingHourFrom);
        Assert.Equal(new TimeOnly(18, 0), row.WorkingHourTo);
        Assert.Equal(user.UserId, row.SysUserModified);
        Assert.NotNull(row.SysDateModified);
        Assert.Equal(new TimeOnly(10, 0), response.ContactPerson?.WorkingHourFrom);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCompanyProfileHandler(db, user)
                .Handle(ProfileTestData.ValidCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesProfile_AsCompanyUser_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCompanyProfileHandler(db, user)
                .Handle(ProfileTestData.ValidCommand(foreignId), CancellationToken.None));

        Assert.Equal(0, await db.CompanyContacts.IgnoreQueryFilters()
            .CountAsync(row => row.CompanyId == foreignId));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesProfile_AsPlatformAdmin_StoresTheRows()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");
        var staffId = await ProfileTestData.SeedStaffAsync(db, foreignId);

        var response = await new UpdateCompanyProfileHandler(db, user)
            .Handle(ProfileTestData.ValidCommand(foreignId, staffId), CancellationToken.None);

        Assert.Equal(foreignId, response.CompanyId);
        Assert.Equal(staffId, response.ContactPerson?.StaffId);
        var row = await db.CompanyContacts.AsNoTracking()
            .SingleAsync(r => r.CompanyId == foreignId);
        Assert.Equal(user.UserId, row.SysUserCreated);
    }

    [Fact]
    public async Task Validator_UnknownContactPerson_Fails()
    {
        var db = await ProfileTestData.CreateDbAsync(ProfileTestData.CompanyUser());
        var validator = new UpdateCompanyProfileValidator(db);

        var result = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            Guid.NewGuid(), contactPersonStaffId: Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Contact person not found.");
    }

    [Fact]
    public async Task Validator_StaffOfAnotherCompany_Fails()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        var foreignCompany = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");
        var foreignStaff = await ProfileTestData.SeedStaffAsync(db, foreignCompany);
        var validator = new UpdateCompanyProfileValidator(db);

        var result = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            user.CompanyId, contactPersonStaffId: foreignStaff));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Contact person not found.");
    }

    [Fact]
    public async Task Validator_UnknownHalalExecutive_Fails()
    {
        var db = await ProfileTestData.CreateDbAsync(ProfileTestData.CompanyUser());
        var validator = new UpdateCompanyProfileValidator(db);

        var result = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            Guid.NewGuid(), halalExecutiveStaffId: Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Halal executive not found.");
    }

    [Fact]
    public async Task Validator_SoftDeletedStaff_Fails()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var staffId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId);
        var staff = await db.Staffs.SingleAsync(row => row.Id == staffId);
        db.Staffs.Remove(staff);
        await db.SaveChangesAsync();
        var validator = new UpdateCompanyProfileValidator(db);

        var result = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            user.CompanyId, contactPersonStaffId: staffId));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Contact person not found.");
    }

    [Fact]
    public async Task Validator_NegativeNumberOfEmployees_Fails()
    {
        var db = await ProfileTestData.CreateDbAsync(ProfileTestData.CompanyUser());
        var validator = new UpdateCompanyProfileValidator(db);

        var result = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            Guid.NewGuid(), numberOfEmployees: -1));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Number of employees cannot be negative.");
    }

    [Fact]
    public async Task Validator_NullStaffIdsAndValidStaff_Pass()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);
        var staffId = await ProfileTestData.SeedStaffAsync(db, user.CompanyId);
        var validator = new UpdateCompanyProfileValidator(db);

        var cleared = await validator.ValidateAsync(
            ProfileTestData.ValidCommand(user.CompanyId));
        var filled = await validator.ValidateAsync(ProfileTestData.ValidCommand(
            user.CompanyId,
            contactPersonStaffId: staffId,
            halalExecutiveStaffId: staffId,
            numberOfEmployees: null));

        Assert.True(cleared.IsValid);
        Assert.True(filled.IsValid);
    }
}
