using VHSmart_Api.Features.Admin.Companies;
using VHSmart_Api.Shared.Exceptions;

namespace VHSmart_Api.Tests.Features.Admin.Companies;

public class GetCompanyOptionsTests
{
    [Fact]
    public async Task Handle_ReturnsD13Lists()
    {
        var handler = new GetCompanyOptionsHandler(CompanyTestData.PlatformUser());

        var response = await handler.Handle(
            new GetCompanyOptionsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "Companies Commission Of Malaysia" },
            response.RegistrationTypes);
        Assert.Equal(new[] { "Muslim Owner" }, response.OwnerStatuses);
        Assert.Equal(new[] { "Medium Small Company" }, response.IndustrySizes);
        Assert.Equal(new[] { "Overseas", "Domestic" }, response.Markets);
    }

    [Fact]
    public async Task Handle_CompanyUser_ThrowsForbidden()
    {
        var handler = new GetCompanyOptionsHandler(CompanyTestData.CompanyUser());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetCompanyOptionsQuery(), CancellationToken.None));
    }
}
