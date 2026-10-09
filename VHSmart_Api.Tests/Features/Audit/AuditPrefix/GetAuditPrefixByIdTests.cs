using VHSmart_Api.Features.Audit.AuditPrefix;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.AuditPrefix.AuditPrefixTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPrefix;

public class GetAuditPrefixByIdTests
{
    [Fact]
    public async Task Handle_ExistingRow_ReturnsBrandIdBrandNameAndModified()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var brandId = await SeedBrandAsync(db, name: "SERUNAI");
        var id = await SeedPrefixAsync(
            db, CompanyA, brandId: brandId, prefix: "SRN", description: "testing 1");

        var response = await new GetAuditPrefixByIdHandler(db).Handle(
            new GetAuditPrefixByIdQuery(id), CancellationToken.None);

        Assert.Equal(id, response.Id);
        Assert.Equal(brandId, response.BrandId);
        Assert.Equal("SERUNAI", response.Brand);
        Assert.Equal("SRN", response.AuditPrefix);
        Assert.Equal("testing 1", response.Description);
        Assert.Null(response.ModifiedDate);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAuditPrefixByIdHandler(db).Handle(
                new GetAuditPrefixByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RowOfAnotherCompany_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var foreignId = await SeedPrefixAsync(db, CompanyB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAuditPrefixByIdHandler(db).Handle(
                new GetAuditPrefixByIdQuery(foreignId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SoftDeletedRow_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var id = await SeedPrefixAsync(db, CompanyA);
        await new DeleteAuditPrefixHandler(db).Handle(
            new DeleteAuditPrefixCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetAuditPrefixByIdHandler(db).Handle(
                new GetAuditPrefixByIdQuery(id), CancellationToken.None));
    }
}
