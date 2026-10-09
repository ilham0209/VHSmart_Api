using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.HalalApplication.CertificateItem;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Tests.Features.HalalApplication.ManageBatch;
using VHSmart_Api.Tests.Features.Product.VerifyHalalProductUpdate;
using static VHSmart_Api.Tests.Features.HalalApplication.CertificateItem.CertificateTestData;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.CertificateItem;

// Spec 12.8 "List of Certificate Item" and its "CLICK TO ADD" -> Add flow: the joined list
// columns, the company-wide certificate-number reuse (Database.md 10) and the ownership
// rules. The popup confirmation is client side, so only the number entry is server work.
public class CertificateItemTests
{
    private static async Task<
        (TestableVHSmartDbContext db, TestCurrentUser user, Guid applicationId, Guid itemId)>
        SeededItemAsync(
            string itemName = "Santan Kicap",
            Guid? certificateId = null,
            Guid? brandId = null,
            Guid? productId = null,
            string referenceNo = "VHS(PR)/01012026/1")
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedCompanyAsync(db, CompanyA);
        var applicationId = await SeedApplicationAsync(
            db,
            CompanyA,
            referenceNo: referenceNo,
            status: ApplicationStatus.ApplicationApproved);
        var itemId = await SeedCertificateItemAsync(
            db, CompanyA, applicationId, itemName,
            brandId: brandId, certificateId: certificateId, productId: productId);
        return (db, user, applicationId, itemId);
    }

    [Fact]
    public async Task List_ReturnsTheJoinedColumns()
    {
        var (db, _, applicationId, itemId) = await SeededItemAsync();
        var brandId = await BatchTestData.SeedBrandAsync(db, name: "Sereni", companyId: CompanyA);
        var productId = await VerifyHalalTestData.SeedProductAsync(
            db, "Santan Kicap", CompanyA, brandId: brandId);
        var certificateId = await SeedCertificateAsync(
            db, CompanyA, applicationId, certificateNo: "HAL-2026-0001");
        var item = await db.CertificateItems.SingleAsync(row => row.Id == itemId);
        item.ProductId = productId;
        item.BrandId = brandId;
        item.HalalCertificateId = certificateId;
        await db.SaveChangesAsync();

        var response = await new GetCertificateItemsHandler(db, UserA())
            .Handle(new GetCertificateItemsQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        Assert.Equal(applicationId, row.ApplicationId);
        Assert.Equal("Sereni Trading Sdn Bhd", row.CompanyName);
        Assert.Equal(
            (await db.Schemes.AsNoTracking()
                .Where(scheme => scheme.Id == db.Applications
                    .Where(app => app.Id == applicationId)
                    .Select(app => app.SchemeId)
                    .Single())
                .Select(scheme => scheme.Name)
                .SingleAsync()),
            row.Scheme);
        Assert.Equal("VHS(PR)/01012026/1", row.ReferenceNo);
        Assert.Equal("Santan Kicap", row.CertificateItem);
        Assert.Equal("Sereni", row.Brand);
        Assert.Equal("HAL-2026-0001", row.CertificateNumber);
    }

    [Fact]
    public async Task List_WithoutCertificateNumber_ReturnsNullForClickToAdd()
    {
        var (db, _, _, _) = await SeededItemAsync();

        var response = await new GetCertificateItemsHandler(db, UserA())
            .Handle(new GetCertificateItemsQuery(), CancellationToken.None);

        var row = Assert.Single(response.Data);
        // Empty -> the client renders the "CLICK TO ADD" link (spec 12.8).
        Assert.Null(row.CertificateNumber);
    }

    [Fact]
    public async Task List_ForeignCompanyRows_AreHidden()
    {
        var (db, _, _, _) = await SeededItemAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        await SeedCertificateItemAsync(db, CompanyB, foreignApplicationId, "Foreign Item");

        var response = await new GetCertificateItemsHandler(db, UserA())
            .Handle(new GetCertificateItemsQuery(), CancellationToken.None);

        Assert.Single(response.Data);
        Assert.Equal("Santan Kicap", Assert.Single(response.Data).CertificateItem);
    }

    [Fact]
    public async Task Add_NewNumber_CreatesTheCertificateRowAndLinksTheItem()
    {
        var (db, _, applicationId, itemId) = await SeededItemAsync();

        var response = await new AddCertificateNumberToItemHandler(db, UserA())
            .Handle(
                new AddCertificateNumberToItemCommand(itemId, "  HAL-2026-0001  "),
                CancellationToken.None);

        Assert.Equal(itemId, response.ItemId);
        Assert.Equal("HAL-2026-0001", response.CertificateNumber);

        var certificate = await db.HalalCertificates.AsNoTracking().SingleAsync();
        Assert.Equal(response.HalalCertificateId, certificate.Id);
        Assert.Equal(CompanyA, certificate.CompanyId);
        Assert.Equal(applicationId, certificate.ApplicationId);
        Assert.Equal("HAL-2026-0001", certificate.CertificateNo);

        var item = await db.CertificateItems.AsNoTracking().SingleAsync();
        Assert.Equal(certificate.Id, item.HalalCertificateId);
    }

    [Fact]
    public async Task Add_SameNumberOnASecondApplication_ReusesTheCompanyRow()
    {
        // UQ (CompanyId, CertificateNo) is company wide: a second application of the same
        // company links to the SAME row instead of creating a twin (flagged stance).
        var (db, _, applicationId, itemId) = await SeededItemAsync();
        var secondApplicationId = await SeedApplicationAsync(
            db, CompanyA, status: ApplicationStatus.ApplicationApproved);
        var secondItemId = await SeedCertificateItemAsync(
            db, CompanyA, secondApplicationId, "Second Item");

        var first = await new AddCertificateNumberToItemHandler(db, UserA())
            .Handle(
                new AddCertificateNumberToItemCommand(itemId, "HAL-2026-0001"),
                CancellationToken.None);
        var second = await new AddCertificateNumberToItemHandler(db, UserA())
            .Handle(
                new AddCertificateNumberToItemCommand(secondItemId, "HAL-2026-0001"),
                CancellationToken.None);

        Assert.Equal(first.HalalCertificateId, second.HalalCertificateId);
        var certificate = await db.HalalCertificates.AsNoTracking().SingleAsync();
        // The row keeps its first application - only the items point at it.
        Assert.Equal(applicationId, certificate.ApplicationId);
    }

    [Fact]
    public async Task Add_NumberWithDifferentCasing_LinksToTheExistingRow()
    {
        var (db, _, applicationId, itemId) = await SeededItemAsync();
        await SeedCertificateAsync(db, CompanyA, applicationId, certificateNo: "hal-2026-0001");

        var response = await new AddCertificateNumberToItemHandler(db, UserA())
            .Handle(
                new AddCertificateNumberToItemCommand(itemId, "HAL-2026-0001"),
                CancellationToken.None);

        Assert.Equal("hal-2026-0001", response.CertificateNumber);
        Assert.Single(db.HalalCertificates);
    }

    [Fact]
    public async Task Add_OnAnAlreadyLinkedItem_RepointsIt_AndKeepsTheOldRow()
    {
        var (db, _, applicationId, itemId) = await SeededItemAsync();
        var firstCertificateId = await SeedCertificateAsync(
            db, CompanyA, applicationId, certificateNo: "OLD-1");
        var secondCertificateId = await SeedCertificateAsync(
            db, CompanyA, applicationId, certificateNo: "NEW-2");

        var response = await new AddCertificateNumberToItemHandler(db, UserA())
            .Handle(
                new AddCertificateNumberToItemCommand(itemId, "NEW-2"),
                CancellationToken.None);

        Assert.Equal(secondCertificateId, response.HalalCertificateId);
        Assert.Equal(2, await db.HalalCertificates.CountAsync());
        Assert.Equal(
            secondCertificateId,
            (await db.CertificateItems.AsNoTracking().SingleAsync()).HalalCertificateId);
        Assert.True(await db.HalalCertificates.AnyAsync(row => row.Id == firstCertificateId));
    }

    [Fact]
    public async Task Add_UnknownOrForeignItem_ThrowsNotFound()
    {
        var (db, _, _, _) = await SeededItemAsync();
        var foreignApplicationId = await SeedApplicationAsync(
            db, CompanyB, status: ApplicationStatus.ApplicationApproved);
        var foreignItemId = await SeedCertificateItemAsync(
            db, CompanyB, foreignApplicationId, "Foreign Item");
        var handler = new AddCertificateNumberToItemHandler(db, UserA());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new AddCertificateNumberToItemCommand(Guid.NewGuid(), "HAL-1"),
            CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new AddCertificateNumberToItemCommand(foreignItemId, "HAL-1"),
            CancellationToken.None));
        Assert.Equal(0, await db.HalalCertificates.CountAsync());
    }

    [Fact]
    public async Task Validator_EmptyNumber_FailsTheRequiredMessage()
    {
        var validator = new AddCertificateNumberToItemValidator();

        var result = await validator.ValidateAsync(
            new AddCertificateNumberToItemCommand(Guid.NewGuid(), ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "Certificate number is required.");
    }

    [Fact]
    public async Task Validator_NumberOver100Chars_FailsTheLengthMessage()
    {
        var validator = new AddCertificateNumberToItemValidator();

        var result = await validator.ValidateAsync(
            new AddCertificateNumberToItemCommand(Guid.NewGuid(), new string('x', 101)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage
                == "Certificate number must be 100 characters or fewer.");
    }
}
