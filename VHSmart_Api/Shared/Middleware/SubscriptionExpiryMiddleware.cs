using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;

namespace VHSmart_Api.Shared.Middleware;

// D-11 (CodingRules 8.3): from the subscription End Date on, every API call answers 403 with
// the machine-readable code SUBSCRIPTION_EXPIRED - no grace period. The exceptions are the
// screens an expired company still needs: Account Setting > Subscription (to see the dates),
// change password and logout. A company with no subscription rows is not blocked (nothing to
// expire - flagged default). Runs after authentication (it reads the JWT company) and before
// authorization. The warning e-mail is a background job, never triggered from a request.
public class SubscriptionExpiryMiddleware(RequestDelegate next, ILogger<SubscriptionExpiryMiddleware> logger)
{
    private const string ProblemContentType = "application/problem+json";
    private const string CodeKey = "code";
    private const string ErrorCode = "SUBSCRIPTION_EXPIRED";
    private const string ExpiredDetail =
        "Your subscription has expired. Please renew your subscription to continue.";

    // D-11: subscription history is matched by this prefix, so the tab and its table both
    // stay reachable; logout does not exist yet (no task builds it) but the path is exempted
    // as D-11 says, so adding the endpoint later cannot lock an expired user in.
    private static readonly string[] ExemptPaths =
    [
        "/api/account/subscription",
        "/api/account/password",
        "/api/auth/logout"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true
            || IsExempt(context.Request.Path)
            || !await IsExpiredAsync(context))
        {
            await next(context);
            return;
        }

        logger.LogDebug(
            "Blocked {Method} {Path}: subscription expired",
            context.Request.Method,
            context.Request.Path);
        await WriteExpiredProblemAsync(context);
    }

    private static bool IsExempt(PathString path) =>
        ExemptPaths.Any(exempt => path.StartsWithSegments(exempt, StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> IsExpiredAsync(HttpContext context)
    {
        var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();

        // No active company in the token = nothing to expire; authorization still applies.
        if (currentUser.CompanyId == Guid.Empty)
            return false;

        var db = context.RequestServices.GetRequiredService<VHSmartDbContext>();

        // Explicit company match: the global tenant filter alone is wide open for a
        // ViewAllCompanies caller, and the soft-delete filter still applies on top.
        var latestEnd = await db.CompanySubscriptions
            .Where(x => x.CompanyId == currentUser.CompanyId)
            .MaxAsync(x => (DateTime?)x.EndDate, context.RequestAborted);

        if (latestEnd is null)
            return false;

        // Expiry shown to the user = End Date - 1 day (spec 6.4, 13), so the company is
        // blocked from the End Date on (D-11) exactly when that display date has passed.
        // Shared rule (CodingRules 11): the 9999-12-31 sentinel never expires.
        var displayExpiry = DateOnly.FromDateTime(latestEnd.Value).AddDays(-1);
        return HalalStatusCalculator.IsExpired(displayExpiry, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private static async Task WriteExpiredProblemAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = ExpiredDetail,
            Instance = context.Request.Path.Value
        };
        // RFC 7807 extension data: serialized as the root-level "code" the frontend switches on.
        problem.Extensions[CodeKey] = ErrorCode;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            (object)problem,
            options: null,
            contentType: ProblemContentType,
            cancellationToken: context.RequestAborted);
    }
}
