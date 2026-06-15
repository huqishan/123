using Module.User.Features.AccountManagement.Models;

namespace Module.User.Features.AccountManagement.Services;

public static class AccountPermissionResolver
{
    public static string GetDisplayName(AccountCatalog catalog, string? permissionId)
    {
        return AccountPermissionDisplay.GetDisplayName(catalog.Permissions, permissionId);
    }

    public static int GetLevel(AccountCatalog catalog, string? permissionId)
    {
        return AccountPermissionDisplay.GetPermissionLevel(catalog.Permissions, permissionId);
    }

    public static bool CanManage(int currentLevel, AccountPermissionProfile permission)
    {
        return AccountPermissionDisplay.CanManageLevel(currentLevel, permission.Level);
    }
}
