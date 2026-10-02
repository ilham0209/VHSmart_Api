using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Personnel.Trainings;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.Personnel.Trainings;

public class GetAllTrainingsTests
{
    [Fact]
    public async Task Handle_DefaultSort_IsNewestFirstWithTypeAndCompany()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        await TrainingTestData.SeedCompanyAsync(db, "Verify Halal Sdn Bhd", user.CompanyId);
        await TrainingTestData.SeedTrainingAsync(
            db, user.CompanyId, "Older Course", new DateTime(2026, 1, 10));
        await TrainingTestData.SeedTrainingAsync(
            db, user.CompanyId, "Newer Course", new DateTime(2026, 6, 20),
            TrainingType.SlaughtermanHalalChecker);

        var grid = await new GetAllTrainingsHandler(db, user)
            .Handle(new GetAllTrainingsQuery(), CancellationToken.None);

        var rows = grid.Data.ToList();
        Assert.Equal(2, grid.TotalRecords);
        Assert.Equal(new[] { "Newer Course", "Older Course" }, rows.Select(row => row.Name));
        Assert.Equal(TrainingType.SlaughtermanHalalChecker, rows[0].TrainingType);
        Assert.Equal("Verify Halal Sdn Bhd", rows[0].Company);
        Assert.Equal(new DateTime(2026, 6, 20), rows[0].TrainingDate);
    }

    [Fact]
    public async Task Handle_SearchTerm_FindsName()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        await TrainingTestData.SeedTrainingAsync(db, user.CompanyId, "Halal Awareness 101");
        await TrainingTestData.SeedTrainingAsync(db, user.CompanyId, "Slaughtering Course");

        var grid = await new GetAllTrainingsHandler(db, user)
            .Handle(new GetAllTrainingsQuery
            {
                Request = new DataGridRequest { SearchTerm = "slaughter" }
            }, CancellationToken.None);

        Assert.Equal("Slaughtering Course", Assert.Single(grid.Data).Name);
    }

    [Fact]
    public async Task Handle_CrossCompanyRows_AreInvisible()
    {
        var userA = TrainingTestData.CompanyUser();
        var userB = TrainingTestData.CompanyUser();
        var databaseName = TestDbFactory.NewDatabaseName();
        var dbA = TestDbFactory.Create(databaseName, userA);
        await dbA.Database.EnsureCreatedAsync();
        var companyA = await TrainingTestData.SeedCompanyAsync(
            dbA, "Company A", userA.CompanyId);
        var companyB = await TrainingTestData.SeedCompanyAsync(
            dbA, "Company B", userB.CompanyId);
        await TrainingTestData.SeedTrainingAsync(dbA, companyA, "Company A training");
        await TrainingTestData.SeedTrainingAsync(dbA, companyB, "Company B training");

        var asCompanyA = await new GetAllTrainingsHandler(dbA, userA)
            .Handle(new GetAllTrainingsQuery(), CancellationToken.None);
        var asCompanyB = await new GetAllTrainingsHandler(
                TestDbFactory.Create(databaseName, userB), userB)
            .Handle(new GetAllTrainingsQuery(), CancellationToken.None);

        Assert.Equal("Company A training", Assert.Single(asCompanyA.Data).Name);
        Assert.Equal("Company B training", Assert.Single(asCompanyB.Data).Name);
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_IsNotListed()
    {
        var user = TrainingTestData.CompanyUser();
        var db = await TrainingTestData.CreateDbAsync(user);
        var trainingId = await TrainingTestData.SeedTrainingAsync(
            db, user.CompanyId, "Doomed course");
        var training = await db.Trainings.SingleAsync(row => row.Id == trainingId);
        db.Trainings.Remove(training);
        await db.SaveChangesAsync();

        var grid = await new GetAllTrainingsHandler(db, user)
            .Handle(new GetAllTrainingsQuery(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
    }
}
