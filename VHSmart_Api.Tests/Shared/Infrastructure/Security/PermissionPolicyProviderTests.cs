using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class PermissionPolicyProviderTests
{
    private static readonly PermissionPolicyProvider Provider =
        new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task GetPolicyAsync_PermissionPolicyName_ReturnsRequirement()
    {
        var policy = await Provider.GetPolicyAsync("Permission:Audit.Recommendation:View");

        var requirement = Assert.IsType<PermissionRequirement>(Assert.Single(policy!.Requirements));
        Assert.Equal("Audit.Recommendation", requirement.Key);
        Assert.Equal(PermissionAction.View, requirement.Action);
    }

    [Theory]
    [InlineData("Permission:Admin.Users:Create", "Admin.Users", PermissionAction.Create)]
    [InlineData("Permission:Admin.Users:Edit", "Admin.Users", PermissionAction.Edit)]
    [InlineData("Permission:Admin.Users:Delete", "Admin.Users", PermissionAction.Delete)]
    public async Task GetPolicyAsync_EachAction_ParsesAction(
        string policyName, string expectedKey, PermissionAction expectedAction)
    {
        var policy = await Provider.GetPolicyAsync(policyName);

        var requirement = Assert.IsType<PermissionRequirement>(Assert.Single(policy!.Requirements));
        Assert.Equal(expectedKey, requirement.Key);
        Assert.Equal(expectedAction, requirement.Action);
    }

    [Fact]
    public async Task GetPolicyAsync_UnknownPolicyName_ReturnsNull()
    {
        var policy = await Provider.GetPolicyAsync("SomeOtherPolicy");

        Assert.Null(policy);
    }

    [Fact]
    public async Task GetPolicyAsync_PermissionNameWithUnknownAction_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Provider.GetPolicyAsync("Permission:Admin.Users:Approve"));
    }

    [Fact]
    public async Task GetPolicyAsync_PermissionNameWithoutKey_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Provider.GetPolicyAsync("Permission:"));
    }

    [Fact]
    public void BuildPolicyName_WithKeyAndAction_ReturnsParseableName()
    {
        var policyName = PermissionPolicyProvider.BuildPolicyName(PermissionKeys.AuditAuditPrefix, PermissionAction.Edit);

        Assert.Equal("Permission:Audit.AuditPrefix:Edit", policyName);
    }
}
