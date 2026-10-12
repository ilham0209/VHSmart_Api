using VHSmart_Api.Features.Audit.NonConformanceList;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.NonConformanceList.NonConformanceTestData;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

public class DetailAndHistoryTests
{
    [Fact]
    public async Task GetById_ReturnsDetailWithLatestBlocks()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var fillerId = await SeedUserAsync(db, Guid.Parse(user.UserId));
        var categoryId = await SeedCategoryAsync(db);
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, NonConformanceSource.Internal,
            auditReferenceNo: "SRN/STORE01/18-11-2026(1)",
            findings: "Pest traces behind the chiller.",
            categoryId: categoryId);
        await SeedEntryAsync(
            db, CompanyA, ncId, CorrectiveActionFilledBy.Auditee, fillerId, 1,
            details: "First pass.", attachmentFileName: "old.pdf");
        await SeedEntryAsync(
            db, CompanyA, ncId, CorrectiveActionFilledBy.Auditee, fillerId, 2,
            details: "Deep clean weekly.", attachmentFileName: "evidence.pdf");
        await SeedEntryAsync(
            db, CompanyA, ncId, CorrectiveActionFilledBy.Auditor, fillerId, 3,
            comment: "Verified on site.", statusSet: "Closed");

        var detail = await new GetNonConformanceByIdHandler(db, user).Handle(
            new GetNonConformanceByIdQuery(ncId), CancellationToken.None);

        Assert.Equal("Internal", detail.Source);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", detail.AuditReferenceNo);
        Assert.Equal("Pest traces behind the chiller.", detail.Findings);
        Assert.Equal("Critical", detail.Category);
        Assert.Equal("Deep clean weekly.", detail.CorrectiveActionDetails);
        Assert.Equal("evidence.pdf", detail.CorrectiveActionAttachmentName);
        Assert.Equal(128, detail.CorrectiveActionAttachmentSizeBytes);
        Assert.Equal("Verified on site.", detail.AuditorComment);
        Assert.Equal("Closed", detail.AuditorStatusSet);
        Assert.Equal(3, detail.LatestVersion);
    }

    [Fact]
    public async Task GetById_NoEntries_ReturnsZeroVersionAndNullBlocks()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var detail = await new GetNonConformanceByIdHandler(db, user).Handle(
            new GetNonConformanceByIdQuery(ncId), CancellationToken.None);

        Assert.Equal(0, detail.LatestVersion);
        Assert.Null(detail.CorrectiveActionDetails);
        Assert.Null(detail.AuditorComment);
        Assert.Null(detail.Category);
    }

    [Fact]
    public async Task GetById_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetNonConformanceByIdHandler(db, user).Handle(
                new GetNonConformanceByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_Foreign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetNonConformanceByIdHandler(db, user).Handle(
                new GetNonConformanceByIdQuery(ncId), CancellationToken.None));
    }

    [Fact]
    public async Task History_ReturnsEntriesInVersionOrderWithFillByNames()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var auditeeId = await SeedUserAsync(db, name: "AUDITEE ONE");
        var auditorId = await SeedUserAsync(db, name: "AUDITOR ONE");
        var ncId = await SeedNonConformanceAsync(db, CompanyA);
        await SeedEntryAsync(
            db, CompanyA, ncId, CorrectiveActionFilledBy.Auditor, auditorId, 2,
            comment: "Verified on site.", statusSet: "Closed");
        await SeedEntryAsync(
            db, CompanyA, ncId, CorrectiveActionFilledBy.Auditee, auditeeId, 1,
            details: "Deep clean weekly.", attachmentFileName: "evidence.pdf");

        var rows = await new GetCorrectiveActionHistoryHandler(db, user).Handle(
            new GetCorrectiveActionHistoryQuery(ncId), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Version);
        Assert.Equal("Auditee", rows[0].Action);
        Assert.Equal("Deep clean weekly.", rows[0].Remarks);
        Assert.Equal("evidence.pdf", rows[0].DocumentName);
        Assert.Equal("AUDITEE ONE", rows[0].FillBy);
        Assert.Equal(2, rows[1].Version);
        Assert.Equal("Auditor", rows[1].Action);
        Assert.Equal("Verified on site.", rows[1].Remarks);
        Assert.Null(rows[1].DocumentName);
        Assert.Equal("AUDITOR ONE", rows[1].FillBy);
    }

    [Fact]
    public async Task History_NoEntries_ReturnsEmpty()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var rows = await new GetCorrectiveActionHistoryHandler(db, user).Handle(
            new GetCorrectiveActionHistoryQuery(ncId), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task History_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCorrectiveActionHistoryHandler(db, user).Handle(
                new GetCorrectiveActionHistoryQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task History_Foreign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetCorrectiveActionHistoryHandler(db, user).Handle(
                new GetCorrectiveActionHistoryQuery(ncId), CancellationToken.None));
    }
}
