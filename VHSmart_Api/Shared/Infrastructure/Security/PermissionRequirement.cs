using Microsoft.AspNetCore.Authorization;

namespace VHSmart_Api.Shared.Infrastructure.Security;

// Carries one screen key + action through the ASP.NET authorization pipeline; evaluated by
// PermissionAuthorizationHandler, built by PermissionPolicyProvider.
public sealed record PermissionRequirement(string Key, PermissionAction Action) : IAuthorizationRequirement;
