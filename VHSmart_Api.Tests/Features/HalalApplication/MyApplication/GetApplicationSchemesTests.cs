using VHSmart_Api.Features.HalalApplication.MyApplication;
using static VHSmart_Api.Tests.Features.HalalApplication.MyApplication.ApplicationTestData;

namespace VHSmart_Api.Tests.Features.HalalApplication.MyApplication;

public class GetApplicationSchemesTests
{
    [Fact]
    public async Task Handle_ReturnsTheNineSeededSchemesInPickerOrder()
    {
        var db = await CreateDbAsync(UserA());

        var schemes = await new GetApplicationSchemesHandler(db)
            .Handle(new GetApplicationSchemesQuery(), CancellationToken.None);

        Assert.Equal(9, schemes.Count);
        // SortOrder = the Database.md 14 seed order: PR, PM, Abattoirs, BG, FM, PL, KO, MD, OEM.
        Assert.Equal(
            new[] { "PR", "PM", null, "BG", "FM", "PL", "KO", "MD", "OEM" },
            schemes.Select(scheme => scheme.Code));
        Assert.Equal(
            "Food and Beverages / Supplement Product",
            schemes[0].Name);
        Assert.Equal("Abattoirs", schemes[2].Name);
    }

    [Fact]
    public async Task Handle_SchemeIds_AreTheSeededRowIds()
    {
        var db = await CreateDbAsync(UserA());
        var productSchemeId = await ProductSchemeIdAsync(db);
        var schemeWithoutCodeId = await SchemeWithoutCodeIdAsync(db);

        var schemes = await new GetApplicationSchemesHandler(db)
            .Handle(new GetApplicationSchemesQuery(), CancellationToken.None);

        Assert.Contains(schemes, scheme => scheme.Id == productSchemeId);
        Assert.Contains(schemes, scheme => scheme.Id == schemeWithoutCodeId);
    }
}
