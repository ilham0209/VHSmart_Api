namespace VHSmart_Api.Shared.Infrastructure.Security;

// The four operations a role can hold on a screen (CodingRules 8.2). A permission is
// PermissionKey + action; an action that was never granted is denied.
public enum PermissionAction
{
    View,
    Create,
    Edit,
    Delete
}
