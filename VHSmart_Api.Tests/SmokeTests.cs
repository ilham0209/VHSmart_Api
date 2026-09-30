using System.Net;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VHSmart_Api.Shared.Infrastructure.Behavior;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Sequences;
using VHSmart_Api.Shared.Infrastructure.Storage;

namespace VHSmart_Api.Tests;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_WhenCalled_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_ResolvesVHSmartDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VHSmartDbContext>();

        Assert.NotNull(db);
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_ResolvesCurrentUserFromJwtClaims()
    {
        using var scope = _factory.Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.IsType<JwtCurrentUser>(user);
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_RegistersValidationBehavior()
    {
        using var scope = _factory.Services.CreateScope();
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<PingRequest, string>>();

        Assert.Contains(behaviors, behavior => behavior is ValidationBehavior<PingRequest, string>);
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_ResolvesFileStorage()
    {
        Assert.IsType<LocalFileStorage>(_factory.Services.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_ResolvesReferenceNumberGenerator()
    {
        using var scope = _factory.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceNumberGenerator>();

        Assert.IsType<ReferenceNumberGenerator>(generator);
    }

    [Fact]
    public void ServiceProvider_WhenBuilt_ResolvesNotificationService()
    {
        using var scope = _factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        Assert.IsType<NotificationService>(notifications);
    }

    private sealed record PingRequest(string Message);
}
