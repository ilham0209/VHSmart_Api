using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.General;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.Admin.Companies;

namespace VHSmart_Api.Tests.Features.CompanyInformation.General;

public class UpdateCompanyGeneralTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesOwnCompanyFields()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user, "Old Name");
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), name: "New Name") with
        { Address1 = "Jalan Baru 1", Market = "Overseas" };

        var response = await new UpdateCompanyGeneralHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(user.CompanyId, response.Id);
        Assert.Equal("New Name", response.Name);
        Assert.Equal("Jalan Baru 1", response.Address1);
        Assert.Equal("Overseas", response.Market);
        Assert.Equal("Old Name CB", response.CertificationBodyName);

        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.Equal("New Name", stored.Name);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_LeavesCertificationBodyIsActiveAndBrandsUntouched()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user, "Locked Columns", isActive: false);
        var brandId = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var original = await db.Companies.SingleAsync(row => row.Id == user.CompanyId);
        var originalCbId = original.CertificationBodyId;
        db.CompanyBrands.Add(new CompanyBrandEntity
        {
            CompanyId = user.CompanyId,
            BrandId = brandId
        });
        await db.SaveChangesAsync();
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), name: "Renamed");

        await new UpdateCompanyGeneralHandler(db, user)
            .Handle(command, CancellationToken.None);

        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.Equal(originalCbId, stored.CertificationBodyId);
        Assert.False(stored.IsActive);
        var link = await db.CompanyBrands.AsNoTracking().SingleAsync();
        Assert.False(link.IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCompanyGeneralHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateBusinessRegistrationNumberOfOtherCompany_ThrowsConflict()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user);
        var otherId = await CompanyGeneralTestData.SeedOtherCompanyAsync(db, "Other Co");
        var otherNumber = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == otherId)
            .Select(company => company.BusinessRegistrationNo)
            .SingleAsync();
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), businessRegistrationNo: otherNumber);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCompanyGeneralHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateEmailOfOtherCompany_ThrowsConflict()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user);
        var otherId = await CompanyGeneralTestData.SeedOtherCompanyAsync(db, "Other Co");
        var otherEmail = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == otherId)
            .Select(company => company.Email)
            .SingleAsync();
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), email: otherEmail);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCompanyGeneralHandler(db, user)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OwnRegistrationNumberAndEmail_AreNotDuplicates()
    {
        var user = CompanyGeneralTestData.CompanyUser();
        var db = await CompanyGeneralTestData.CreateDbAsync(user);
        await CompanyGeneralTestData.SeedOwnCompanyAsync(db, user);
        var own = await db.Companies
            .AsNoTracking()
            .SingleAsync(company => company.Id == user.CompanyId);
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            email: own.Email,
            businessRegistrationNo: own.BusinessRegistrationNo);

        var response = await new UpdateCompanyGeneralHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Equal(user.CompanyId, response.Id);
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CompanyGeneralTestData.CreateDbAsync(CompanyGeneralTestData.CompanyUser());
        var validator = new UpdateCompanyGeneralValidator(db);
        var command = CompanyGeneralTestData.ValidCommand(Guid.Empty) with
        {
            Name = string.Empty,
            RegistrationType = string.Empty,
            BusinessRegistrationNo = string.Empty,
            OwnerStatus = string.Empty,
            Address1 = string.Empty,
            Address2 = string.Empty,
            PostCode = string.Empty,
            District = string.Empty,
            State = string.Empty,
            Telephone = string.Empty,
            Email = string.Empty
        };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        var messages = result.Errors.Select(failure => failure.ErrorMessage).ToArray();
        Assert.Contains("Name is required.", messages);
        Assert.Contains("Registration type is required.", messages);
        Assert.Contains("Business registration number is required.", messages);
        Assert.Contains("Owner status is required.", messages);
        Assert.Contains("Address line 1 is required.", messages);
        Assert.Contains("Address line 2 is required.", messages);
        Assert.Contains("Postcode is required.", messages);
        Assert.Contains("District is required.", messages);
        Assert.Contains("State is required.", messages);
        Assert.Contains("Country is required.", messages);
        Assert.Contains("Telephone is required.", messages);
        Assert.Contains("Email is required.", messages);
    }

    [Fact]
    public async Task Validator_UnknownCountry_Fails()
    {
        var db = await CompanyGeneralTestData.CreateDbAsync(CompanyGeneralTestData.CompanyUser());
        var validator = new UpdateCompanyGeneralValidator(db);
        var command = CompanyGeneralTestData.ValidCommand(Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Country not found.");
    }

    [Fact]
    public async Task Validator_UnknownScheme_Fails()
    {
        var db = await CompanyGeneralTestData.CreateDbAsync(CompanyGeneralTestData.CompanyUser());
        var validator = new UpdateCompanyGeneralValidator(db);
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), schemeId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Scheme not found.");
    }

    [Fact]
    public async Task Validator_DateOfEstablishmentNotBeforeToday_Fails()
    {
        var db = await CompanyGeneralTestData.CreateDbAsync(CompanyGeneralTestData.CompanyUser());
        var validator = new UpdateCompanyGeneralValidator(db);
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);

        var today = await validator.ValidateAsync(
            CompanyGeneralTestData.ValidCommand(
                countryId, dateOfEstablishment: DateTime.UtcNow.Date));
        var future = await validator.ValidateAsync(
            CompanyGeneralTestData.ValidCommand(
                countryId, dateOfEstablishment: DateTime.UtcNow.Date.AddDays(1)));

        Assert.False(today.IsValid);
        Assert.Contains(
            today.Errors,
            failure => failure.ErrorMessage
                == "Date of establishment must be strictly earlier than today.");
        Assert.False(future.IsValid);
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CompanyGeneralTestData.CreateDbAsync(CompanyGeneralTestData.CompanyUser());
        var validator = new UpdateCompanyGeneralValidator(db);
        var command = CompanyGeneralTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
