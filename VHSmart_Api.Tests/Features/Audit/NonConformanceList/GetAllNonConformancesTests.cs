using VHSmart_Api.Features.Audit.NonConformanceList;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Models;
using static VHSmart_Api.Tests.Features.Audit.NonConformanceList.NonConformanceTestData;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

public class GetAllNonConformancesTests
{
    private static GetAllNonConformancesQuery Query(
        DataGridRequest? request = null,
        Guid? auditTaskId = null,
        bool finalOnly = false) => new()
        {
            Request = request ?? new DataGridRequest(),
            AuditTaskId = auditTaskId,
            FinalOnly = finalOnly
        };

    [Fact]
    public async Task Handle_ReturnsRowsWithCompanyName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var internalNc = await SeedNonConformanceAsync(
            db, CompanyA, NonConformanceSource.Internal,
            auditReferenceNo: "SRN/STORE01/18-11-2026(1)",
            findings: "Pest traces behind the chiller.",
            tags: "pest");
        await SeedNonConformanceAsync(db, CompanyA);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(2, grid.TotalRecords);
        Assert.Contains(grid.Data, row => row.Id == internalNc
            && row.Source == "Internal"
            && row.CompanyName == "HALAL CO"
            && row.Findings == "Pest traces behind the chiller."
            && row.Tags == "pest");
        Assert.All(grid.Data, row => Assert.Equal("HALAL CO", row.CompanyName));
    }

    [Fact]
    public async Task Handle_DefaultSort_IsSourceAscending()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var internalNc = await SeedNonConformanceAsync(db, CompanyA, NonConformanceSource.Internal);
        var externalNc = await SeedNonConformanceAsync(db, CompanyA, NonConformanceSource.External);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(), CancellationToken.None);

        // "External" sorts before "Internal" alphabetically.
        Assert.Equal([externalNc, internalNc], grid.Data.Select(row => row.Id));
    }

    [Fact]
    public async Task Handle_SearchTerm_FiltersAcrossColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedNonConformanceAsync(
            db, CompanyA, findings: "Pest traces behind the chiller.");
        var miss = await SeedNonConformanceAsync(
            db, CompanyA, findings: "Temperature log missing.");

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(new DataGridRequest { SearchTerm = "Pest traces" }), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.DoesNotContain(grid.Data, row => row.Id == miss);
    }

    [Fact]
    public async Task Handle_FinalOnly_ReturnsFinalizedOnly()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var final = await SeedNonConformanceAsync(db, CompanyA, isFinal: true);
        await SeedNonConformanceAsync(db, CompanyA, isFinal: false);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(finalOnly: true), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal(final, Assert.Single(grid.Data).Id);
    }

    [Fact]
    public async Task Handle_AuditTaskIdFilter_ReturnsThatTasksInternalsOnly()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (taskId, itemId) = await SeedTaskWithItemAsync(db, CompanyA);
        var fed = await SeedNonConformanceAsync(
            db, CompanyA, NonConformanceSource.Internal, auditTaskItemId: itemId);
        var loose = await SeedNonConformanceAsync(db, CompanyA, NonConformanceSource.Internal);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(auditTaskId: taskId), CancellationToken.None);

        Assert.Equal(1, grid.TotalRecords);
        Assert.Equal(fed, Assert.Single(grid.Data).Id);
        Assert.DoesNotContain(grid.Data, row => row.Id == loose);
    }

    [Fact]
    public async Task Handle_OtherCompany_SeesNothing()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedNonConformanceAsync(db, CompanyB);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(), CancellationToken.None);

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }

    [Fact]
    public async Task Handle_SortDescending_WhenRequested()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var externalNc = await SeedNonConformanceAsync(db, CompanyA, NonConformanceSource.External);
        var internalNc = await SeedNonConformanceAsync(db, CompanyA, NonConformanceSource.Internal);

        var grid = await new GetAllNonConformancesHandler(db, user).Handle(
            Query(new DataGridRequest { SortBy = "Source", SortDescending = true }),
            CancellationToken.None);

        Assert.Equal([internalNc, externalNc], grid.Data.Select(row => row.Id));
    }
}
