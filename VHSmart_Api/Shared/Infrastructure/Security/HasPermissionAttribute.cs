using Microsoft.AspNetCore.Authorization;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Authorization filter for one screen (CodingRules 5 and 8.2): every action carries it, and the
// class it decorates carries [Authorize] so an anonymous caller is challenged (401) before the
// permission is evaluated (403).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string key, PermissionAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Policy = PermissionPolicyProvider.BuildPolicyName(key, action);
    }
}
