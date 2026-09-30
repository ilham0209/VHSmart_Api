using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Tests.Shared.Infrastructure.Security;

public class PermissionAuthorizationHandlerTests
{
    private static readonly Guid RoleId = Guid.NewGuid();
    private const string Key = "Audit.Recommendation";

    [Fact]
    public async Task HandleAsync_PermissionGranted_Succeeds()
    {
        var context = Run(new StubCurrentUser(RoleId), authenticated: true, granted: true);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_PermissionDenied_DoesNotSucceed()
    {
        var context = Run(new StubCurrentUser(RoleId), authenticated: true, granted: false);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NoRoleInToken_DoesNotSucceed()
    {
        var context = Run(new StubCurrentUser(Guid.Empty), authenticated: true, granted: true);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Unauthenticated_DoesNotSucceed()
    {
        var context = Run(new StubCurrentUser(RoleId), authenticated: false, granted: true);

        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext Run(ICurrentUser currentUser, bool authenticated, bool granted)
    {
        var identity = new ClaimsIdentity(authenticated ? "Test" : null);
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(Key, PermissionAction.View);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        var permissionService = new StubPermissionService(
            granted ? [(RoleId, Key, PermissionAction.View)] : []);

        new PermissionAuthorizationHandler(permissionService, currentUser)
            .HandleAsync(context)
            .GetAwaiter()
            .GetResult();

        return context;
    }

    private sealed record StubCurrentUser(Guid RoleId) : ICurrentUser
    {
        public string UserId { get; init; } = string.Empty;

        public Guid CompanyId { get; init; } = Guid.Empty;

        public bool IsPlatformAdmin { get; init; }

        public bool ViewAllCompanies { get; init; }
    }
}
