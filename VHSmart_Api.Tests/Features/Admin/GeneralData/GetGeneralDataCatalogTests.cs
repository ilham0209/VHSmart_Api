using VHSmart_Api.Features.Admin.GeneralData;
using VHSmart_Api.Shared.Domain.Admin;

namespace VHSmart_Api.Tests.Features.Admin.GeneralData;

public class GetGeneralDataCatalogTests
{
    [Fact]
    public async Task Handle_ReturnsAllSevenGroupsInSpecOrder()
    {
        var result = await new GetGeneralDataCatalogHandler()
            .Handle(new GetGeneralDataCatalogQuery(), CancellationToken.None);

        Assert.Equal(
            new[]
            {
                GeneralDataGroup.COMPANY, GeneralDataGroup.PEOPLE, GeneralDataGroup.PRODUCT,
                GeneralDataGroup.CERTIFICATES, GeneralDataGroup.PAYMENT, GeneralDataGroup.AUDIT,
                GeneralDataGroup.TRAINING
            },
            result.Select(item => item.Group).ToArray());
    }

    [Fact]
    public async Task Handle_ReturnsTwentySevenCategoryPairs()
    {
        var result = await new GetGeneralDataCatalogHandler()
            .Handle(new GetGeneralDataCatalogQuery(), CancellationToken.None);

        Assert.Equal(27, result.Sum(item => item.Categories.Count));
    }

    [Fact]
    public async Task Handle_GroupCategoryLists_MatchSpecFivePointOne()
    {
        var result = await new GetGeneralDataCatalogHandler()
            .Handle(new GetGeneralDataCatalogQuery(), CancellationToken.None);

        Assert.Equal(9, Assert.Single(result, item => item.Group == GeneralDataGroup.COMPANY).Categories.Count);
        Assert.Equal(
            ["Title of Honour", "Designation", "Department", "Internal Halal Committee Role"],
            Assert.Single(result, item => item.Group == GeneralDataGroup.PEOPLE).Categories);
        Assert.Equal(
            ["Ingredient Source", "Ingredient Status", "Product Category", "Marketing Method"],
            Assert.Single(result, item => item.Group == GeneralDataGroup.PRODUCT).Categories);
        Assert.Equal(
            ["Certificate Status"],
            Assert.Single(result, item => item.Group == GeneralDataGroup.CERTIFICATES).Categories);
        Assert.Equal(
            ["Payment Category"],
            Assert.Single(result, item => item.Group == GeneralDataGroup.PAYMENT).Categories);
        // Audit carries the seven categories of spec 14.1.
        Assert.Equal(
            [
                "Audit Type", "Audit Purpose", "Non Compliance Category",
                "External - Audit Reference", "External - CB Non Conformance Details",
                "External - Audit Category", "Internal - Audit Category"
            ],
            Assert.Single(result, item => item.Group == GeneralDataGroup.AUDIT).Categories);
        Assert.Equal(
            ["Module Type"],
            Assert.Single(result, item => item.Group == GeneralDataGroup.TRAINING).Categories);
    }

    [Fact]
    public void BelongsTo_AcceptsCataloguePair_CaseInsensitively()
    {
        Assert.True(GeneralDataCatalog.BelongsTo(GeneralDataGroup.COMPANY, "Brand"));
        Assert.True(GeneralDataCatalog.BelongsTo(GeneralDataGroup.COMPANY, "brand"));
    }

    [Fact]
    public void BelongsTo_RejectsPairFromAnotherGroupOrUnknownCategory()
    {
        Assert.False(GeneralDataCatalog.BelongsTo(GeneralDataGroup.COMPANY, "Audit Type"));
        Assert.False(GeneralDataCatalog.BelongsTo(GeneralDataGroup.AUDIT, "Brand"));
        Assert.False(GeneralDataCatalog.BelongsTo(GeneralDataGroup.PEOPLE, "Something Else"));
    }
}
