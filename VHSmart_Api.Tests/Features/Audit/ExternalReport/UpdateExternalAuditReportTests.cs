using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.ExternalReport.ExternalReportTestData;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class UpdateExternalAuditReportTests
{
    private static UpdateExternalAuditReportHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ValidCommand_UpdatesHeaderAndRebuildsPremises()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premiseOne) =
            await SeedHeaderReferencesAsync(db);
        var premiseTwo = await SeedPremiseAsync(db, CompanyA, "Kedai Serai Corner");
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premiseOne]);

        var response = await Handler(db, user).Handle(
            new UpdateExternalAuditReportCommand(
                reportId, cb, "HALAL-NEW-REF", typeOfApplication, typeOfInspection, scheme,
                null, null, new DateOnly(2025, 12, 1),
                new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0),
                "Updated remarks", "Pending", [premiseTwo]),
            CancellationToken.None);

        Assert.Equal("HALAL-NEW-REF", response.CbAuditReferenceNo);
        Assert.Equal(new DateOnly(2025, 12, 1), response.AuditDate);
        Assert.Equal("Pending", response.OverallStatus);
        var link = Assert.Single(response.Premises);
        Assert.Equal("Kedai Serai Corner", link.Name);

        // Old links are soft-deleted (AU-06 member rebuild), the picked set re-inserted.
        var liveLinks = await db.ExternalAuditReportPremises
            .IgnoreQueryFilters()
            .Where(row => row.ExternalAuditReportId == reportId)
            .ToListAsync();
        Assert.Equal(2, liveLinks.Count);
        Assert.Single(liveLinks, row => !row.IsDeleted);
        Assert.Single(liveLinks, row => row.IsDeleted);
    }

    [Fact]
    public async Task Handle_UnknownReport_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateExternalAuditReportCommand(
                    Guid.NewGuid(), cb, "REF", typeOfApplication, typeOfInspection, scheme,
                    null, null, new DateOnly(2025, 12, 1),
                    new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0),
                    null, "Pass", [premise]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignReport_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyB, cb, scheme, [premise]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db, user).Handle(
                new UpdateExternalAuditReportCommand(
                    reportId, cb, "REF", typeOfApplication, typeOfInspection, scheme,
                    null, null, new DateOnly(2025, 12, 1),
                    new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0),
                    null, "Pass", [premise]),
                CancellationToken.None));
    }
}
