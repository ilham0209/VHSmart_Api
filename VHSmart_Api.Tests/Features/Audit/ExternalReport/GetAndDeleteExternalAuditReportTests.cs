using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.ExternalReport.ExternalReportTestData;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class GetAndDeleteExternalAuditReportTests
{
    [Fact]
    public async Task GetById_Valid_ReturnsDetailWithJoinedNames()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premiseOne) = await SeedHeaderReferencesAsync(db);
        var premiseTwo = await SeedPremiseAsync(db, CompanyA, "Kedai Serai Corner");
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premiseTwo, premiseOne]);

        var response = await new GetExternalAuditReportByIdHandler(db, user).Handle(
            new GetExternalAuditReportByIdQuery(reportId), CancellationToken.None);

        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", response.ReceivedFrom);
        Assert.Equal("Halal Food", response.ApplicationScheme);
        Assert.Equal("HALAL-20251013-131025(PM)", response.CbAuditReferenceNo);
        Assert.Equal(new DateOnly(2025, 11, 29), response.AuditDate);
        Assert.Equal(2, response.Premises.Count);
        // Premises come back name-ordered (display order).
        Assert.Equal("Kedai Serai Corner", response.Premises[0].Name);
        Assert.Equal("Seri Rasa Factory", response.Premises[1].Name);
    }

    [Fact]
    public async Task GetById_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetExternalAuditReportByIdHandler(db, user).Handle(
                new GetExternalAuditReportByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_Foreign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyB, cb, scheme, [premise]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetExternalAuditReportByIdHandler(db, user).Handle(
                new GetExternalAuditReportByIdQuery(reportId), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_Valid_SoftDeletesReportAndChildren()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var ncId = await SeedNonConformanceAsync(db, CompanyA, reportId);
        var attachmentId = await SeedAttachmentAsync(db, CompanyA, reportId);

        await new DeleteExternalAuditReportHandler(db, user).Handle(
            new DeleteExternalAuditReportCommand(reportId), CancellationToken.None);

        // Soft delete all round; children first (AU-06 stance).
        var report = await db.ExternalAuditReports.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == reportId);
        Assert.True(report.IsDeleted);
        var nc = await db.NonConformances.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == ncId);
        Assert.True(nc.IsDeleted);
        var attachment = await db.ExternalAuditReportAttachments.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == attachmentId);
        Assert.True(attachment.IsDeleted);
        var link = await db.ExternalAuditReportPremises.IgnoreQueryFilters()
            .SingleAsync(row => row.ExternalAuditReportId == reportId);
        Assert.True(link.IsDeleted);
    }

    [Fact]
    public async Task Delete_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteExternalAuditReportHandler(db, user).Handle(
                new DeleteExternalAuditReportCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
