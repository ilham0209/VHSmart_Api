using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Resolves the policy name produced by HasPermissionAttribute ("Permission:{key}:{action}") into
// a policy that carries a PermissionRequirement. Registered in place of
// DefaultAuthorizationPolicyProvider, which AddAuthorization only TryAdds - so it must be
// registered before AddAuthorization.
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "Permission:";

    public static string BuildPolicyName(string key, PermissionAction action) =>
        $"{Prefix}{key}:{action}";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(policyName);

        var parts = policyName.Split(':', 3);
        if (parts.Length != 3
            || string.IsNullOrWhiteSpace(parts[1])
            || !Enum.TryParse<PermissionAction>(parts[2], out var action))
            throw new InvalidOperationException($"'{policyName}' is not a valid permission policy name.");

        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(parts[1], action))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
