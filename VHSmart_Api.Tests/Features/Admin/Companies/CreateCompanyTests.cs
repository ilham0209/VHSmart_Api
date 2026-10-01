using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class CreateCompanyTests
{
    [Fact]
    public async Task Handle_ValidCommand_StoresCompanyAndBrandLinks()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);
        var cbId = await CompanyTestData.SeedCertificationBodyAsync(db);
        var rtw = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var serunai = await CompanyTestData.SeedBrandAsync(db, "SERUNAI");

        var response = await new CreateCompanyHandler(db, user)
            .Handle(
                CompanyTestData.ValidCommand(countryId, cbId, rtw) with
                {
                    BrandIds = [rtw, serunai]
                },
                CancellationToken.None);

        Assert.Equal("Acme Foods", response.Name);
        Assert.True(response.IsActive);
        Assert.Equal(["RTW", "SERUNAI"], response.Brands.Select(brand => brand.Name));

        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.Equal(response.Id, stored.Id);
        Assert.Equal("Selangor", stored.State);
        Assert.True(stored.IsActive);
        Assert.False(stored.IsDeleted);
        Assert.Equal(user.UserId, stored.SysUserCreated);

        var links = await db.CompanyBrands.AsNoTracking().ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(stored.Id, link.CompanyId));
    }

    [Fact]
    public async Task Handle_WithoutBrandIds_CreatesCompanyWithoutLinks()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db));

        var response = await new CreateCompanyHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.Empty(response.Brands);
        Assert.Empty(await db.CompanyBrands.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Handle_IsActiveFalse_StoresInactive()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db)) with
        { IsActive = false };

        var response = await new CreateCompanyHandler(db, user)
            .Handle(command, CancellationToken.None);

        Assert.False(response.IsActive);
        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Handle_DuplicateBusinessRegistrationNo_ThrowsConflict()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);
        var cbId = await CompanyTestData.SeedCertificationBodyAsync(db);
        var first = CompanyTestData.ValidCommand(countryId, cbId);
        await new CreateCompanyHandler(db, user).Handle(first, CancellationToken.None);

        var second = CompanyTestData.ValidCommand(
            countryId, cbId, businessRegistrationNo: first.BusinessRegistrationNo);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateCompanyHandler(db, user).Handle(second, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);
        var cbId = await CompanyTestData.SeedCertificationBodyAsync(db);
        var first = CompanyTestData.ValidCommand(countryId, cbId);
        await new CreateCompanyHandler(db, user).Handle(first, CancellationToken.None);

        var second = CompanyTestData.ValidCommand(countryId, cbId, email: first.Email);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateCompanyHandler(db, user).Handle(second, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var companyUser = CompanyTestData.CompanyUser();
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CreateCompanyHandler(db, companyUser)
                .Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Validator_MissingRequiredFields_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);
        var cbId = await CompanyTestData.SeedCertificationBodyAsync(db);
        var validator = new CreateCompanyValidator(db);
        var invalid = CompanyTestData.ValidCommand(countryId, cbId) with
        {
            Name = string.Empty,
            RegistrationType = null,
            BusinessRegistrationNo = string.Empty,
            OwnerStatus = null,
            Address1 = string.Empty,
            Address2 = string.Empty,
            PostCode = string.Empty,
            District = string.Empty,
            State = string.Empty,
            Telephone = string.Empty,
            Email = string.Empty
        };

        var result = await validator.ValidateAsync(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Name");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "RegistrationType");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "BusinessRegistrationNo");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "OwnerStatus");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Address1");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Address2");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "PostCode");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "District");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "State");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Telephone");
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Email");
    }

    [Fact]
    public async Task Validator_UnknownCertificationBody_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db), Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CertificationBodyId"
                && failure.ErrorMessage == "Certification body not found.");
    }

    [Fact]
    public async Task Validator_UnknownCountry_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            Guid.NewGuid(), await CompanyTestData.SeedCertificationBodyAsync(db));

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "CountryId"
                && failure.ErrorMessage == "Country not found.");
    }

    [Fact]
    public async Task Validator_UnknownScheme_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            schemeId: Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "SchemeId"
                && failure.ErrorMessage == "Scheme not found.");
    }

    [Fact]
    public async Task Validator_UnknownBrand_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.StartsWith("BrandIds", StringComparison.Ordinal)
                && failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_NonBrandGeneralDataRow_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var notABrand = new GeneralDataEntity
        {
            CompanyId = Guid.NewGuid(),
            Group = GeneralDataGroup.COMPANY,
            Category = "Ownership Type",
            Name = "Sole Proprietor"
        };
        db.GeneralData.Add(notABrand);
        await db.SaveChangesAsync();
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            notABrand.Id);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Brand not found.");
    }

    [Fact]
    public async Task Validator_DateOfEstablishmentNotBeforeToday_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            dateOfEstablishment: DateTime.UtcNow.Date);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "DateOfEstablishment"
                && failure.ErrorMessage
                    == "Date of establishment must be strictly earlier than today.");
    }

    [Fact]
    public async Task Validator_FullValidCommand_Passes()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var schemeId = await db.Schemes.Select(scheme => scheme.Id).FirstAsync();
        var brandId = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var validator = new CreateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            brandId,
            schemeId: schemeId);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(failure => failure.ErrorMessage)));
    }
}
