using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.NonConformanceList;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.Product.ManageProduct;
using static VHSmart_Api.Tests.Features.Audit.NonConformanceList.NonConformanceTestData;

namespace VHSmart_Api.Tests.Features.Audit.NonConformanceList;

public class CorrectiveActionTests
{
    private static SaveCorrectiveActionHandler SaveHandler(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        StubFileStorage? storage = null) =>
        new(db, user, storage ?? new StubFileStorage());

    private static IFormFile DocumentFile(
        string fileName = "action.pdf",
        byte[]? content = null,
        long? declaredLength = null)
    {
        var bytes = content ?? [37, 80, 68, 70];
        return new FormFile(
            new MemoryStream(bytes), 0, declaredLength ?? bytes.Length, "File", fileName);
    }

    [Fact]
    public async Task Save_Valid_SubmitsNcAndCreatesVersionOne()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var fillerId = await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var response = await SaveHandler(db, user).Handle(
            new SaveCorrectiveActionCommand(ncId, "Deep clean weekly.", null),
            CancellationToken.None);

        Assert.Equal(1, response.Version);
        Assert.Equal("Auditee", response.FilledBy);
        Assert.Equal("Deep clean weekly.", response.Details);
        var nc = await db.NonConformances.SingleAsync(row => row.Id == ncId);
        Assert.Equal(NonConformanceStatus.CorrectiveActionSubmitted, nc.Status);
        var entry = await db.CorrectiveActionEntries.SingleAsync(row => row.Id == response.Id);
        Assert.Equal(fillerId, entry.FilledByUserId);
    }

    [Fact]
    public async Task Save_WithAttachment_StoresFileOnEntry()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(db, CompanyA);
        var storage = new StubFileStorage();

        var response = await SaveHandler(db, user, storage).Handle(
            new SaveCorrectiveActionCommand(ncId, "Fixed.", DocumentFile("evidence.pdf")),
            CancellationToken.None);

        Assert.Equal("evidence.pdf", response.DocumentName);
        Assert.Equal(4, response.DocumentSizeBytes);
        var entry = await db.CorrectiveActionEntries.SingleAsync(row => row.Id == response.Id);
        Assert.Equal("evidence.pdf", entry.Attachment!.FileName);
        Assert.Equal(4, entry.Attachment.SizeBytes);
    }

    [Fact]
    public async Task Save_WhileSubmitted_AddsVersionTwo()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, status: NonConformanceStatus.CorrectiveActionSubmitted);
        var handler = SaveHandler(db, user);

        await handler.Handle(
            new SaveCorrectiveActionCommand(ncId, "First pass.", null), CancellationToken.None);
        var second = await handler.Handle(
            new SaveCorrectiveActionCommand(ncId, "Second pass.", null), CancellationToken.None);

        Assert.Equal(2, second.Version);
        var nc = await db.NonConformances.SingleAsync(row => row.Id == ncId);
        Assert.Equal(NonConformanceStatus.CorrectiveActionSubmitted, nc.Status);
    }

    [Fact]
    public async Task Save_OnClosedNc_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, status: NonConformanceStatus.Closed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(ncId, "Too late.", null), CancellationToken.None));

        Assert.Equal("The non-conformance is closed.", exception.Message);
    }

    [Fact]
    public async Task Save_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(Guid.NewGuid(), "x", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Save_Foreign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(ncId, "x", null), CancellationToken.None));
    }

    [Fact]
    public async Task Save_InvalidUserId_ThrowsBusinessRule()
    {
        var user = InvalidIdUser();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(ncId, "x", null), CancellationToken.None));

        Assert.Equal("User id is invalid.", exception.Message);
    }

    [Fact]
    public async Task Save_DisallowedExtension_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(ncId, "x", DocumentFile("malware.exe")),
                CancellationToken.None));

        Assert.StartsWith("File type is not allowed.", exception.Message);
    }

    [Fact]
    public async Task Save_OversizeFile_ThrowsBusinessRule()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            SaveHandler(db, user).Handle(
                new SaveCorrectiveActionCommand(
                    ncId, "x", DocumentFile(declaredLength: 11 * 1024 * 1024)),
                CancellationToken.None));

        Assert.StartsWith("The file size exceeds the limit", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingDetails_Fails(string? details)
    {
        var results = await new SaveCorrectiveActionValidator().ValidateAsync(
            new SaveCorrectiveActionCommand(Guid.NewGuid(), details!, null));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors,
            error => error.ErrorMessage == "Corrective Action Details is required.");
    }

    [Fact]
    public async Task Validator_TooLongDetails_Fails()
    {
        var results = await new SaveCorrectiveActionValidator().ValidateAsync(
            new SaveCorrectiveActionCommand(Guid.NewGuid(), new string('x', 2001), null));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors,
            error => error.ErrorMessage
                == "Corrective Action Details cannot exceed 2000 characters.");
    }

    [Fact]
    public async Task Review_Valid_ClosesNcAndStampsLifecycleColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var reviewerId = await SeedUserAsync(db, Guid.Parse(user.UserId), "AUDITOR ONE");
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, status: NonConformanceStatus.CorrectiveActionSubmitted);

        var response = await new ReviewCorrectiveActionHandler(db, user).Handle(
            new ReviewCorrectiveActionCommand(ncId, "Verified on site.", "Closed"),
            CancellationToken.None);

        Assert.Equal("Auditor", response.FilledBy);
        Assert.Equal("Closed", response.StatusSet);
        Assert.Equal(1, response.Version);
        var nc = await db.NonConformances.SingleAsync(row => row.Id == ncId);
        Assert.Equal(NonConformanceStatus.Closed, nc.Status);
        Assert.NotNull(nc.ClosedAt);
        Assert.Equal(reviewerId, nc.ClosedBy);
    }

    [Fact]
    public async Task Review_BeforeSubmission_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(db, CompanyA);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new ReviewCorrectiveActionHandler(db, user).Handle(
                new ReviewCorrectiveActionCommand(ncId, "c", "Closed"), CancellationToken.None));

        Assert.Equal("The corrective action has not been submitted yet.", exception.Message);
    }

    [Fact]
    public async Task Review_OnClosedNc_ThrowsConflict()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedUserAsync(db, Guid.Parse(user.UserId));
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, status: NonConformanceStatus.Closed);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new ReviewCorrectiveActionHandler(db, user).Handle(
                new ReviewCorrectiveActionCommand(ncId, "c", "Closed"), CancellationToken.None));

        Assert.Equal("The non-conformance is closed.", exception.Message);
    }

    [Fact]
    public async Task Review_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReviewCorrectiveActionHandler(db, user).Handle(
                new ReviewCorrectiveActionCommand(Guid.NewGuid(), "c", "Closed"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Review_Foreign_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(
            db, CompanyB, status: NonConformanceStatus.CorrectiveActionSubmitted);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReviewCorrectiveActionHandler(db, user).Handle(
                new ReviewCorrectiveActionCommand(ncId, "c", "Closed"), CancellationToken.None));
    }

    [Fact]
    public async Task Review_InvalidUserId_ThrowsBusinessRule()
    {
        var user = InvalidIdUser();
        var db = await CreateDbAsync(user);
        var ncId = await SeedNonConformanceAsync(
            db, CompanyA, status: NonConformanceStatus.CorrectiveActionSubmitted);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new ReviewCorrectiveActionHandler(db, user).Handle(
                new ReviewCorrectiveActionCommand(ncId, "c", "Closed"), CancellationToken.None));

        Assert.Equal("User id is invalid.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingComment_Fails(string? comment)
    {
        var results = await new ReviewCorrectiveActionValidator().ValidateAsync(
            new ReviewCorrectiveActionCommand(Guid.NewGuid(), comment!, "Closed"));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Comment is required.");
    }

    [Fact]
    public async Task Validator_TooLongComment_Fails()
    {
        var results = await new ReviewCorrectiveActionValidator().ValidateAsync(
            new ReviewCorrectiveActionCommand(Guid.NewGuid(), new string('x', 2001), "Closed"));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Comment cannot exceed 2000 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingStatus_Fails(string? status)
    {
        var results = await new ReviewCorrectiveActionValidator().ValidateAsync(
            new ReviewCorrectiveActionCommand(Guid.NewGuid(), "c", status!));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Status is required.");
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("CorrectiveActionSubmitted")]
    public async Task Validator_StatusOtherThanClosed_Fails(string status)
    {
        var results = await new ReviewCorrectiveActionValidator().ValidateAsync(
            new ReviewCorrectiveActionCommand(Guid.NewGuid(), "c", status));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Status must be Closed.");
    }
}
