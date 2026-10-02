using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.CompanyInformation.Profiles;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.CompanyInformation.Profiles;

public class GetCompanyProfileTests
{
    [Fact]
    public async Task Handle_ExistingProfile_ReturnsBothPeopleHoursAndHeadcount()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var companyId = await ProfileTestData.SeedCompanyAsync(db, "Acme Foods");
        var contactId = await ProfileTestData.SeedStaffAsync(db, companyId, "Aiman Rahman");
        var executiveId = await ProfileTestData.SeedStaffAsync(
            db, companyId, "Nurul Islam", "Manager");
        await ProfileTestData.SeedContactAsync(
            db, companyId, CompanyContactKind.ContactPerson, contactId,
            new TimeOnly(9, 0), new TimeOnly(17, 30));
        await ProfileTestData.SeedContactAsync(
            db, companyId, CompanyContactKind.HalalExecutive, executiveId,
            new TimeOnly(8, 0), new TimeOnly(17, 0));
        var company = await db.Companies.SingleAsync(row => row.Id == companyId);
        company.NumberOfEmployees = 42;
        await db.SaveChangesAsync();

        var response = await new GetCompanyProfileHandler(db, user)
            .Handle(new GetCompanyProfileQuery(companyId), CancellationToken.None);

        Assert.Equal(companyId, response.CompanyId);
        Assert.Equal("Acme Foods", response.CompanyName);
        Assert.Equal("Acme Foods CB", response.CertificationBodyName);
        Assert.Equal(42, response.NumberOfEmployees);

        Assert.NotNull(response.ContactPerson);
        Assert.Equal(contactId, response.ContactPerson.StaffId);
        Assert.Equal("Aiman Rahman", response.ContactPerson.Name);
        Assert.Equal("Halal Executive", response.ContactPerson.Designation);
        Assert.Equal(new TimeOnly(9, 0), response.ContactPerson.WorkingHourFrom);
        Assert.Equal(new TimeOnly(17, 30), response.ContactPerson.WorkingHourTo);

        Assert.NotNull(response.HalalExecutive);
        Assert.Equal(executiveId, response.HalalExecutive.StaffId);
        Assert.Equal("Nurul Islam", response.HalalExecutive.Name);
        Assert.Equal("Manager", response.HalalExecutive.Designation);
        Assert.False(string.IsNullOrWhiteSpace(response.HalalExecutive.Email));
    }

    [Fact]
    public async Task Handle_CompanyWithoutProfile_ReturnsEmptySlots()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Acme Foods", user.CompanyId);

        var response = await new GetCompanyProfileHandler(db, user)
            .Handle(new GetCompanyProfileQuery(user.CompanyId), CancellationToken.None);

        Assert.Null(response.ContactPerson);
        Assert.Null(response.HalalExecutive);
        Assert.Null(response.NumberOfEmployees);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyProfileHandler(db, user)
                .Handle(new GetCompanyProfileQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesProfile_AsCompanyUser_ThrowsNotFound()
    {
        var user = ProfileTestData.CompanyUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        await ProfileTestData.SeedCompanyAsync(db, "Own Foods", user.CompanyId);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyProfileHandler(db, user)
                .Handle(new GetCompanyProfileQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AnotherCompaniesProfile_AsPlatformAdmin_ReturnsIt()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var foreignId = await ProfileTestData.SeedCompanyAsync(db, "Foreign Foods");

        var response = await new GetCompanyProfileHandler(db, user)
            .Handle(new GetCompanyProfileQuery(foreignId), CancellationToken.None);

        Assert.Equal(foreignId, response.CompanyId);
        Assert.Equal("Foreign Foods", response.CompanyName);
    }

    [Fact]
    public async Task Handle_SoftDeletedCompany_ThrowsNotFound()
    {
        var user = ProfileTestData.PlatformUser();
        var db = await ProfileTestData.CreateDbAsync(user);
        var companyId = await ProfileTestData.SeedCompanyAsync(db, "Gone Foods");
        var company = await db.Companies.SingleAsync(row => row.Id == companyId);
        db.Companies.Remove(company);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCompanyProfileHandler(db, user)
                .Handle(new GetCompanyProfileQuery(companyId), CancellationToken.None));
    }
}
