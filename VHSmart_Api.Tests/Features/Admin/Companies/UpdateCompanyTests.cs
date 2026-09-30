using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class UpdateCompanyTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesColumns()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Old Name");
        var countryId = await CompanyTestData.MalaysiaIdAsync(db);
        var cbId = await CompanyTestData.SeedCertificationBodyAsync(db, "New CB");
        var command = CompanyTestData.ValidCommand(
            countryId, cbId, name: "New Name") with
        { Address1 = "Jalan Baru 1" };

        var response = await new UpdateCompanyHandler(db, user)
            .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None);

        Assert.Equal(companyId, response.Id);
        Assert.Equal("New Name", response.Name);
        Assert.Equal("Jalan Baru 1", response.Address1);

        var stored = await db.Companies.AsNoTracking().SingleAsync();
        Assert.Equal("New Name", stored.Name);
        Assert.Equal(user.UserId, stored.SysUserModified);
        Assert.NotNull(stored.SysDateModified);
    }

    [Fact]
    public async Task Handle_BrandDiff_AddsAndSoftDeletesLinks()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var kept = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var dropped = await CompanyTestData.SeedBrandAsync(db, "SERUNAI");
        var added = await CompanyTestData.SeedBrandAsync(db, "NATURAL");
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Brand Co", [kept, dropped]);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db)) with
        {
            BrandIds = [kept, added]
        };

        var response = await new UpdateCompanyHandler(db, user)
            .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None);

        Assert.Equal(["NATURAL", "RTW"], response.Brands.Select(brand => brand.Name));

        var links = await db.CompanyBrands
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();
        Assert.Equal(3, links.Count);
        var droppedLink = Assert.Single(links, link => link.BrandId == dropped);
        Assert.True(droppedLink.IsDeleted);
        Assert.Equal(2, links.Count(link => !link.IsDeleted));
    }

    [Fact]
    public async Task Handle_WithoutBrandIds_RemovesEveryLink()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var brandId = await CompanyTestData.SeedBrandAsync(db, "RTW");
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Brand Co", [brandId]);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db));

        var response = await new UpdateCompanyHandler(db, user)
            .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None);

        Assert.Empty(response.Brands);
        var link = await db.CompanyBrands.IgnoreQueryFilters().SingleAsync();
        Assert.True(link.IsDeleted);
    }

    [Fact]
    public async Task Handle_OmittedIsActive_KeepsCurrentFlag()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "Inactive Co");
        var seeded = await db.Companies.SingleAsync(company => company.Id == companyId);
        seeded.IsActive = false;
        await db.SaveChangesAsync();
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db));

        var response = await new UpdateCompanyHandler(db, user)
            .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None);

        Assert.False(response.IsActive);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCompanyHandler(db, user)
                .Handle(CompanyTestData.ToUpdate(Guid.NewGuid(), command), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var companyUser = CompanyTestData.CompanyUser();
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new UpdateCompanyHandler(db, companyUser)
                .Handle(CompanyTestData.ToUpdate(Guid.NewGuid(), command), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DuplicateEmailOfOtherCompany_ThrowsConflict()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var takenByOther = await CompanyTestData.SeedCompanyAsync(db, "Other Co");
        var otherEmail = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == takenByOther)
            .Select(company => company.Email)
            .SingleAsync();
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "My Co");
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            email: otherEmail);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateCompanyHandler(db, user)
                .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OwnEmail_IsNotADuplicate()
    {
        var user = CompanyTestData.PlatformUser();
        var db = await CompanyTestData.CreateDbAsync(user);
        var companyId = await CompanyTestData.SeedCompanyAsync(db, "My Co");
        var ownEmail = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.Email)
            .SingleAsync();
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            email: ownEmail);

        var response = await new UpdateCompanyHandler(db, user)
            .Handle(CompanyTestData.ToUpdate(companyId, command), CancellationToken.None);

        Assert.Equal(companyId, response.Id);
    }

    [Fact]
    public async Task Validator_UnknownBrand_Fails()
    {
        var db = await CompanyTestData.CreateDbAsync(CompanyTestData.PlatformUser());
        var validator = new UpdateCompanyValidator(db);
        var command = CompanyTestData.ValidCommand(
            await CompanyTestData.MalaysiaIdAsync(db),
            await CompanyTestData.SeedCertificationBodyAsync(db),
            Guid.NewGuid());

        var result = await validator.ValidateAsync(
            CompanyTestData.ToUpdate(Guid.NewGuid(), command));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.StartsWith("BrandIds", StringComparison.Ordinal)
                && failure.ErrorMessage == "Brand not found.");
    }
}
