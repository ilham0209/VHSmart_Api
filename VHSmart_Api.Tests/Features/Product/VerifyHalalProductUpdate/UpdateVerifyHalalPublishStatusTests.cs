using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Product.VerifyHalalProductUpdate;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate.VerifyHalalTestData;

namespace VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;

public class UpdateVerifyHalalPublishStatusTests
{
    [Fact]
    public async Task Handle_Published_StoresTheValue()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductAsync(db);

        await new UpdateVerifyHalalPublishStatusHandler(db, UserA()).Handle(
            new UpdateVerifyHalalPublishStatusCommand(id, VerifyHalalPublishStatus.Published),
            CancellationToken.None);

        var row = await db.Products.SingleAsync(p => p.Id == id);
        Assert.Equal(VerifyHalalPublishStatus.Published, row.VerifyHalalPublishStatus);
    }

    [Fact]
    public async Task Handle_PublishedWithoutImage_StoresTheValue()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductAsync(db);

        await new UpdateVerifyHalalPublishStatusHandler(db, UserA()).Handle(
            new UpdateVerifyHalalPublishStatusCommand(
                id, VerifyHalalPublishStatus.PublishedWithoutImage),
            CancellationToken.None);

        var row = await db.Products.SingleAsync(p => p.Id == id);
        Assert.Equal(VerifyHalalPublishStatus.PublishedWithoutImage, row.VerifyHalalPublishStatus);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateVerifyHalalPublishStatusHandler(db, UserA()).Handle(
                new UpdateVerifyHalalPublishStatusCommand(
                    Guid.NewGuid(), VerifyHalalPublishStatus.Published),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ForeignProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductAsync(db, companyId: CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateVerifyHalalPublishStatusHandler(db, UserA()).Handle(
                new UpdateVerifyHalalPublishStatusCommand(id, VerifyHalalPublishStatus.Published),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedProduct_ThrowsNotFound()
    {
        var db = await CreateDbAsync(UserA());
        var id = await SeedProductAsync(db);
        var row = await db.Products.SingleAsync(p => p.Id == id);
        row.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateVerifyHalalPublishStatusHandler(db, UserA()).Handle(
                new UpdateVerifyHalalPublishStatusCommand(id, VerifyHalalPublishStatus.Published),
                CancellationToken.None));
    }

    [Fact]
    public async Task Validator_EmptyId_Fails()
    {
        var validator = new UpdateVerifyHalalPublishStatusValidator();

        var result = await validator.ValidateAsync(
            new UpdateVerifyHalalPublishStatusCommand(Guid.Empty, VerifyHalalPublishStatus.Published));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "Id");
    }

    [Fact]
    public async Task Validator_EmptyStatus_FailsWithSpecMessage()
    {
        var validator = new UpdateVerifyHalalPublishStatusValidator();

        var result = await validator.ValidateAsync(
            new UpdateVerifyHalalPublishStatusCommand(Guid.NewGuid(), string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "PublishStatus"
                && failure.ErrorMessage == "Publish status is required.");
    }

    [Fact]
    public async Task Validator_NullStatus_Fails()
    {
        var validator = new UpdateVerifyHalalPublishStatusValidator();

        var result = await validator.ValidateAsync(
            new UpdateVerifyHalalPublishStatusCommand(Guid.NewGuid(), null!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == "PublishStatus");
    }

    [Fact]
    public async Task Validator_UnknownStatus_FailsWithSpecMessage()
    {
        var validator = new UpdateVerifyHalalPublishStatusValidator();

        var result = await validator.ValidateAsync(
            new UpdateVerifyHalalPublishStatusCommand(Guid.NewGuid(), "Draft"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == "PublishStatus"
                && failure.ErrorMessage == "Publish status is invalid.");
    }

    [Theory]
    [InlineData(VerifyHalalPublishStatus.Published)]
    [InlineData(VerifyHalalPublishStatus.PublishedWithoutImage)]
    public async Task Validator_KnownStatus_Passes(string status)
    {
        var validator = new UpdateVerifyHalalPublishStatusValidator();

        var result = await validator.ValidateAsync(
            new UpdateVerifyHalalPublishStatusCommand(Guid.NewGuid(), status));

        Assert.True(result.IsValid);
    }
}
