using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.CompanyInformation.Ihc;
using VHSmart_Api.Tests.Features.CompanyInformation.Profiles;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetApplicationByIdTests
{
    [Fact]
    public async Task Handle_KnownRow_ReturnsTheHeaderAndSurveyBlock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            referenceNo: "VHS(PR)/01012026/1",
            applicationType: "Renewal",
            cbApplicationNo: "CB-2026-001");

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(applicationId, response.Id);
        Assert.Equal("VHS(PR)/01012026/1", response.ReferenceNo);
        Assert.Equal(ApplicationStatus.Draft, response.Status);
        Assert.Equal("Renewal", response.ApplicationType);
        Assert.Equal("CB-2026-001", response.CbApplicationNo);
        Assert.Equal("Sereni Trading Sdn Bhd", response.CompanyName);
        // The D-08 survey answers every stored application carries (spec 12.5 General
        // Information shows them read-only).
        Assert.True(response.Survey.ReadProcedureManual);
        Assert.True(response.Survey.ReadMs1500);
        Assert.False(response.Survey.HandlesProhibited);
        Assert.True(response.Survey.HasIhc);
        Assert.Null(response.BatchId);
        Assert.Null(response.BatchName);
        Assert.Null(response.ContactPerson);
        Assert.Null(response.HalalExecutive);
        Assert.Empty(response.IhcMembers);
        Assert.Empty(response.AdditionalInformation);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetApplicationByIdHandler(db, user)
                .Handle(new GetApplicationByIdQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new GetApplicationByIdHandler(db, user)
                .Handle(new GetApplicationByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BatchOfTheApplication_FillsTheBatchName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var batchId = await SeedBatchAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA, batchId: batchId);

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(batchId, response.BatchId);
        Assert.Equal("Santan Batch Pertama", response.BatchName);
    }

    [Fact]
    public async Task Handle_CompanyBlock_FillsTheProfileCertificationBodyAndScheme()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        // The full profile block the spec 12.5 Company Information tab pre-fills from, with
        // its own certification body (spec 6.1) and the product scheme as the company scheme.
        var companyId = await ProfileTestData.SeedCompanyAsync(
            db, "Sereni Trading Sdn Bhd", companyId: CompanyA);
        var company = await db.Companies.SingleAsync(row => row.Id == companyId);
        company.SchemeId = await ProductSchemeIdAsync(db);
        await db.SaveChangesAsync();
        var applicationId = await SeedApplicationAsync(db, companyId);

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(companyId, response.Company.CompanyId);
        Assert.Equal("Sereni Trading Sdn Bhd", response.Company.CompanyName);
        Assert.Equal("Sereni Trading Sdn Bhd CB", response.Company.CertificationBody.Name);
        Assert.Equal("Companies Commission Of Malaysia", response.Company.RegistrationType);
        Assert.Equal("Muslim Owner", response.Company.OwnerStatus);
        Assert.Equal("Jalan Perusahaan 1", response.Company.Address1);
        Assert.Equal("50000", response.Company.PostCode);
        Assert.Equal("Selangor", response.Company.State);
        Assert.Equal("0300000000", response.Company.Telephone);
        Assert.NotNull(response.Company.BusinessRegistrationNo);
        Assert.Equal(
            "Food and Beverages / Supplement Product", response.Company.SchemeName);
    }

    [Fact]
    public async Task Handle_CompanyRowMissing_ReturnsAnEmptyCompanyBlock()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(string.Empty, response.Company.CompanyName);
        Assert.Equal(string.Empty, response.Company.CertificationBody.Name);
        Assert.Equal(string.Empty, response.Company.BusinessRegistrationNo);
    }

    [Fact]
    public async Task Handle_Contacts_AreProjectedFromTheStaffRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var contactPersonStaff = await ProfileTestData.SeedStaffAsync(
            db, CompanyA, name: "Aiman Rahman", designation: "Operation Manager");
        var halalExecutiveStaff = await ProfileTestData.SeedStaffAsync(
            db, CompanyA, name: "Siti Aminah", designation: "Halal Executive");
        await ProfileTestData.SeedContactAsync(
            db,
            CompanyA,
            CompanyContactKind.ContactPerson,
            contactPersonStaff,
            new TimeOnly(9, 0),
            new TimeOnly(17, 30));
        await ProfileTestData.SeedContactAsync(
            db,
            CompanyA,
            CompanyContactKind.HalalExecutive,
            halalExecutiveStaff);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        var contactPerson = Assert.IsType<ApplicationContactResponse>(response.ContactPerson);
        Assert.Equal("Aiman Rahman", contactPerson.Name);
        Assert.Equal("Operation Manager", contactPerson.Designation);
        Assert.Equal(new TimeOnly(9, 0), contactPerson.WorkingHourFrom);
        Assert.Equal(new TimeOnly(17, 30), contactPerson.WorkingHourTo);

        var halalExecutive = Assert.IsType<ApplicationContactResponse>(
            response.HalalExecutive);
        Assert.Equal("Siti Aminah", halalExecutive.Name);
        Assert.Equal("Halal Executive", halalExecutive.Designation);
        Assert.Null(halalExecutive.WorkingHourFrom);
    }

    [Fact]
    public async Task Handle_IhcMembers_OnlyFlaggedOnesWithARoleOrderedByRoleName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        await MinutesMeetingTestData.SeedStaffAsync(
            db, CompanyA, name: "Zarith Zainal", ihcMember: true, ihcRole: "Secretary");
        await MinutesMeetingTestData.SeedStaffAsync(
            db, CompanyA, name: "Amir Hakim", ihcMember: true, ihcRole: "Chairperson");
        await MinutesMeetingTestData.SeedStaffAsync(db, CompanyA, name: "Bulk Member");
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(2, response.IhcMembers.Count);
        Assert.Equal("Amir Hakim", response.IhcMembers[0].Name);
        Assert.Equal("Chairperson", response.IhcMembers[0].IhcRole);
        Assert.Equal("Zarith Zainal", response.IhcMembers[1].Name);
        Assert.Equal("Secretary", response.IhcMembers[1].IhcRole);
    }

    [Fact]
    public async Task Handle_AdditionalInformation_ReturnsTheRowsSorted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.QualityControl, "HACCP");
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.Packaging, "OTHERS", "Glass jar");
        await SeedAdditionalInfoItemAsync(
            db, CompanyA, applicationId,
            ApplicationAdditionalInfoSection.Packaging, "CARTON_BOX");

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal(3, response.AdditionalInformation.Count);
        Assert.Equal(ApplicationAdditionalInfoSection.Packaging,
            response.AdditionalInformation[0].Section);
        Assert.Equal("CARTON_BOX", response.AdditionalInformation[0].OptionCode);
        Assert.Equal("OTHERS", response.AdditionalInformation[1].OptionCode);
        Assert.Equal("Glass jar", response.AdditionalInformation[1].FreeText);
        Assert.Equal(ApplicationAdditionalInfoSection.QualityControl,
            response.AdditionalInformation[2].Section);
        Assert.Equal("HACCP", response.AdditionalInformation[2].OptionCode);
    }

    [Fact]
    public async Task Handle_ExtrasOfTheCompanyTab_AreReturned()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);
        var application = await db.Applications.SingleAsync(
            row => row.Id == applicationId);
        application.YearlySalesRevenue = "1200000";
        application.ProductMarket = "International";
        application.WorkingHourFrom = new TimeOnly(8, 0);
        application.WorkingHourTo = new TimeOnly(18, 0);
        application.NumberOfShifts = 2;
        application.MuslimManagement = 3;
        application.NonMuslimChef = 4;
        await db.SaveChangesAsync();

        var response = await new GetApplicationByIdHandler(db, user)
            .Handle(new GetApplicationByIdQuery(applicationId), CancellationToken.None);

        Assert.Equal("1200000", response.Extras.YearlySalesRevenue);
        Assert.Equal("International", response.Extras.ProductMarket);
        Assert.Equal(new TimeOnly(8, 0), response.Extras.WorkingHourFrom);
        Assert.Equal(new TimeOnly(18, 0), response.Extras.WorkingHourTo);
        Assert.Equal(2, response.Extras.NumberOfShifts);
        Assert.Equal(3, response.Extras.MuslimManagement);
        Assert.Equal(4, response.Extras.NonMuslimChef);
        Assert.Null(response.Extras.MuslimChef);
    }
}
