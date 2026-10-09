using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.MyApplication;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class UpdateCompanyInformationTests
{
    private static UpdateCompanyInformationCommand Command(
        Guid id,
        string? yearlySalesRevenue = null,
        string? productMarket = null,
        TimeOnly? workingHourFrom = null,
        TimeOnly? workingHourTo = null,
        int? numberOfShifts = null,
        int? muslimManagement = null,
        int? muslimFoodHandler = null,
        int? muslimChef = null,
        int? nonMuslimManagement = null,
        int? nonMuslimFoodHandler = null,
        int? nonMuslimChef = null) =>
        new(
            id, yearlySalesRevenue, productMarket, workingHourFrom, workingHourTo,
            numberOfShifts, muslimManagement, muslimFoodHandler, muslimChef,
            nonMuslimManagement, nonMuslimFoodHandler, nonMuslimChef);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesTheExtras()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new UpdateCompanyInformationHandler(db, user)
            .Handle(
                Command(
                    applicationId,
                    yearlySalesRevenue: "2500000",
                    productMarket: "international",
                    workingHourFrom: new TimeOnly(8, 30),
                    workingHourTo: new TimeOnly(17, 30),
                    numberOfShifts: 3,
                    muslimManagement: 5,
                    muslimFoodHandler: 12,
                    muslimChef: 4,
                    nonMuslimManagement: 1,
                    nonMuslimFoodHandler: 8,
                    nonMuslimChef: 2),
                CancellationToken.None);

        Assert.Equal("2500000", response.YearlySalesRevenue);
        // Stored with the picker's casing, whatever casing the payload carried.
        Assert.Equal("International", response.ProductMarket);
        Assert.Equal(new TimeOnly(8, 30), response.WorkingHourFrom);
        Assert.Equal(new TimeOnly(17, 30), response.WorkingHourTo);
        Assert.Equal(3, response.NumberOfShifts);
        Assert.Equal(5, response.MuslimManagement);
        Assert.Equal(12, response.MuslimFoodHandler);
        Assert.Equal(4, response.MuslimChef);
        Assert.Equal(1, response.NonMuslimManagement);
        Assert.Equal(8, response.NonMuslimFoodHandler);
        Assert.Equal(2, response.NonMuslimChef);

        var stored = await db.Applications.SingleAsync(row => row.Id == applicationId);
        Assert.Equal("International", stored.ProductMarket);
        Assert.Equal(3, stored.NumberOfShifts);
    }

    [Fact]
    public async Task Handle_BlankProductMarket_IsStoredAsNull()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(db, CompanyA);

        var response = await new UpdateCompanyInformationHandler(db, user)
            .Handle(
                Command(applicationId, productMarket: "   "),
                CancellationToken.None);

        Assert.Null(response.ProductMarket);
        Assert.Null((await db.Applications.SingleAsync()).ProductMarket);
    }

    [Fact]
    public async Task Handle_ForeignApplication_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedApplicationAsync(db, CompanyB);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => new UpdateCompanyInformationHandler(db, user)
                .Handle(Command(foreignId), CancellationToken.None));

        Assert.Equal("Application not found.", exception.Message);
    }

    [Fact]
    public async Task Handle_SubmittedApplication_ThrowsTheD26ReadOnlyRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db, CompanyA, status: ApplicationStatus.ApplicationApproved);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new UpdateCompanyInformationHandler(db, user)
                .Handle(Command(applicationId, numberOfShifts: 1), CancellationToken.None));

        Assert.Equal(
            "Submitted applications cannot be edited except for status tagging.",
            exception.Message);
        Assert.Null((await db.Applications.SingleAsync()).NumberOfShifts);
    }

    [Fact]
    public async Task Validator_ValidCommand_Passes()
    {
        var validator = new UpdateCompanyInformationValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), productMarket: "Domestic"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1, null, null, null, null, null, null)]
    [InlineData(null, -1, null, null, null, null, null)]
    [InlineData(null, null, -1, null, null, null, null)]
    [InlineData(null, null, null, -1, null, null, null)]
    [InlineData(null, null, null, null, -1, null, null)]
    [InlineData(null, null, null, null, null, -1, null)]
    [InlineData(null, null, null, null, null, null, -1)]
    public async Task Validator_NegativeCounter_FailsWithTheSingleRule(
        int? numberOfShifts,
        int? muslimManagement,
        int? muslimFoodHandler,
        int? muslimChef,
        int? nonMuslimManagement,
        int? nonMuslimFoodHandler,
        int? nonMuslimChef)
    {
        var validator = new UpdateCompanyInformationValidator();

        var result = await validator.ValidateAsync(
            Command(
                Guid.NewGuid(),
                numberOfShifts: numberOfShifts,
                muslimManagement: muslimManagement,
                muslimFoodHandler: muslimFoodHandler,
                muslimChef: muslimChef,
                nonMuslimManagement: nonMuslimManagement,
                nonMuslimFoodHandler: nonMuslimFoodHandler,
                nonMuslimChef: nonMuslimChef));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Employee counts and number of shifts must be 0 or more.");
    }

    [Fact]
    public async Task Validator_UnknownProductMarket_Fails()
    {
        var validator = new UpdateCompanyInformationValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), productMarket: "Export"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Product market must be Domestic or International.");
    }

    [Fact]
    public async Task Validator_NullProductMarket_Passes()
    {
        var validator = new UpdateCompanyInformationValidator();

        var result = await validator.ValidateAsync(Command(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_YearlySalesRevenueTooLong_Fails()
    {
        var validator = new UpdateCompanyInformationValidator();

        var result = await validator.ValidateAsync(
            Command(Guid.NewGuid(), yearlySalesRevenue: new string('x', 101)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Yearly sales revenue must be 100 characters or fewer.");
    }
}
