using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.AuditPlanning;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditPlanning.AuditPlanningTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

public class CreateAuditPlanTests
{
    private static CreateAuditPlanHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user, CreateGenerator(db));

    private static async Task<(Guid Purpose, Guid Type, Guid Category)> SeedDropdownsAsync(
        TestableVHSmartDbContext db,
        Guid? companyId = null) =>
        (
            await SeedAuditPurposeAsync(db, companyId),
            await SeedAuditTypeAsync(db, companyId),
            await SeedPlanningCategoryAsync(db, companyId));

    [Fact]
    public async Task Handle_ValidCommand_CreatesPlanAndAutoCreatesNotStartedTask()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        var prefix = await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        var schedule = new DateOnly(2026, 11, 18);

        var response = await Handler(db, user).Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, schedule, group),
            CancellationToken.None);

        // D-09: {AuditPrefix}/{StoreCode}/{dd-MM-yyyy}({n}).
        Assert.Equal("SRN/STORE01/18-11-2026(1)", response.AuditReferenceNo);

        var plan = await db.AuditPlans.SingleAsync(row => row.Id == response.Id);
        Assert.Equal(CompanyA, plan.CompanyId);
        Assert.Equal(purpose, plan.AuditPurposeId);
        Assert.Equal(type, plan.AuditTypeId);
        Assert.Equal(category, plan.CategoryId);
        Assert.Equal(premise, plan.PremiseId);
        Assert.Equal(schedule.ToDateTime(TimeOnly.MinValue), plan.ScheduleDate);
        Assert.Equal(group, plan.GroupAuditorId);
        // The template is NOT user-chosen: the group's single checklist is copied.
        Assert.Equal(checklist, plan.ChecklistId);
        Assert.Equal(Guid.Parse(user.UserId), plan.AssignedByUserId);

        // Spec 14.9 flow step 1: saving the plan auto-creates the task.
        var task = await db.AuditTasks.SingleAsync(row => row.AuditPlanId == plan.Id);
        Assert.Equal(CompanyA, task.CompanyId);
        Assert.Equal(AuditTaskStatus.NotStarted, task.Status);
        Assert.Equal(0m, task.AuditScore);
        Assert.Equal(0m, task.FinalReviewScore);
        Assert.Equal(task.Id, response.AuditTaskId);
    }

    [Fact]
    public async Task Handle_PremiseWithoutStoreCode_UsesThePremiseName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(
            db, CompanyA, name: "Seri Rasa Factory", storeCode: null, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        var response = await Handler(db, user).Handle(
            new CreateAuditPlanCommand(
                purpose, type, category, premise, new DateOnly(2026, 11, 18), group),
            CancellationToken.None);

        Assert.Equal("SRN/Seri Rasa Factory/18-11-2026(1)", response.AuditReferenceNo);
    }

    [Fact]
    public async Task Handle_SecondPlanSamePrefixAndDate_RunningNumberIncrements()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        var schedule = new DateOnly(2026, 11, 18);
        var handler = Handler(db, user);

        var first = await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, schedule, group),
            CancellationToken.None);
        var second = await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, schedule, group),
            CancellationToken.None);

        Assert.Equal("SRN/STORE01/18-11-2026(1)", first.AuditReferenceNo);
        Assert.Equal("SRN/STORE01/18-11-2026(2)", second.AuditReferenceNo);
    }

    [Fact]
    public async Task Handle_DifferentScheduleDate_RestartsAtOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        var handler = Handler(db, user);

        await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, new DateOnly(2026, 11, 18), group),
            CancellationToken.None);
        var nextDay = await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, new DateOnly(2026, 11, 19), group),
            CancellationToken.None);

        Assert.Equal("SRN/STORE01/19-11-2026(1)", nextDay.AuditReferenceNo);
    }

    [Fact]
    public async Task Handle_PremiseWithoutBrand_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: null);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, user).Handle(
                new CreateAuditPlanCommand(
                    purpose, type, category, premise, new DateOnly(2026, 11, 18), group),
                CancellationToken.None));

        Assert.Equal("Audit Prefix is not set for the premise's brand.", exception.Message);
    }

    [Fact]
    public async Task Handle_BrandWithoutPrefix_ThrowsBusinessRuleWithBrandName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db, name: "SERUNAI");
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, user).Handle(
                new CreateAuditPlanCommand(
                    purpose, type, category, premise, new DateOnly(2026, 11, 18), group),
                CancellationToken.None));

        Assert.Equal("Audit Prefix is not set for brand SERUNAI.", exception.Message);
    }

    [Fact]
    public async Task Handle_ForeignPrefixRow_DoesNotSatisfyTheCaller()
    {
        // The prefix is looked up under the CALLER's company - another tenant's row for the
        // same brand id never leaks in.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db, companyId: CompanyB);
        await SeedPrefixAsync(db, CompanyB, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Handler(db, user).Handle(
                new CreateAuditPlanCommand(
                    purpose, type, category, premise, new DateOnly(2026, 11, 18), group),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PastScheduleDate_IsAllowed()
    {
        // The spec does not rule past dates ([VERIFY], not invented).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        var response = await Handler(db, user).Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, new DateOnly(2020, 1, 15), group),
            CancellationToken.None);

        Assert.Equal("SRN/STORE01/15-01-2020(1)", response.AuditReferenceNo);
    }

    [Fact]
    public async Task Handle_SamePremiseSameDayTwice_IsAllowed()
    {
        // No uniqueness rule beyond UQ AuditReferenceNo ([VERIFY], not invented) - the two
        // plans differ by their running number.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        var schedule = new DateOnly(2026, 11, 18);
        var handler = Handler(db, user);

        await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, schedule, group),
            CancellationToken.None);
        await handler.Handle(
            new CreateAuditPlanCommand(purpose, type, category, premise, schedule, group),
            CancellationToken.None);

        Assert.Equal(2, await db.AuditPlans.CountAsync());
    }

    [Fact]
    public async Task Validator_MissingPurpose_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Purpose is required.");
    }

    [Fact]
    public async Task Validator_ForeignPurpose_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignPurpose = await SeedAuditPurposeAsync(db, CompanyB);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            foreignPurpose, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Purpose not found.");
    }

    [Fact]
    public async Task Validator_ForeignType_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db);
        var foreignType = await SeedAuditTypeAsync(db, CompanyB);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, foreignType, Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Type not found.");
    }

    [Fact]
    public async Task Validator_ForeignCategory_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db);
        var type = await SeedAuditTypeAsync(db);
        var foreignCategory = await SeedPlanningCategoryAsync(db, CompanyB);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, foreignCategory, Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_WrongAuditPairAsCategory_Fails()
    {
        // Category must be an "Internal - Audit Category" row - the AUDIT group alone is
        // not enough.
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db);
        var type = await SeedAuditTypeAsync(db);
        var wrongPair = await SeedNonPlanningCategoryAsync(db);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, wrongPair, Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Validator_ForeignPremise_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var foreignPremise = await SeedPremiseAsync(db, CompanyB);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, category, foreignPremise,
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Premise not found.");
    }

    [Fact]
    public async Task Validator_EmptyScheduleDate_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, category, Guid.NewGuid(), default, Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Schedule Date is required.");
    }

    [Fact]
    public async Task Validator_InactiveGroup_Fails()
    {
        // Only groups with status Active can be selected (spec 14.7/14.8 [CONFIRMED]).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var checklist = await SeedChecklistAsync(db);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var inactiveGroup = await SeedGroupAsync(
            db, CompanyA, checklist, leader, status: GroupAuditorStatus.Inactive);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, category, Guid.NewGuid(),
            new DateOnly(2026, 11, 18), inactiveGroup));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Auditor is not active.");
    }

    [Fact]
    public async Task Validator_ForeignGroup_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (purpose, type, category) = await SeedDropdownsAsync(db);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        var foreignGroup = await SeedGroupAsync(db, CompanyB, checklist, leader);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, type, category, Guid.NewGuid(),
            new DateOnly(2026, 11, 18), foreignGroup));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Group Auditor not found.");
    }

    [Fact]
    public async Task Validator_MissingTypeAndCategory_Fail()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db);
        var validator = new CreateAuditPlanValidator(db, user);

        var result = await validator.ValidateAsync(new CreateAuditPlanCommand(
            purpose, Guid.Empty, Guid.Empty, Guid.NewGuid(),
            new DateOnly(2026, 11, 18), Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Audit Type is required.");
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Category is required.");
    }
}
