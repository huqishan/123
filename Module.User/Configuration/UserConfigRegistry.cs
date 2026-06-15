using System;
using System.IO;

namespace Module.User.Configuration;

public static class UserConfigRegistry
{
    public static string ConfigDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Config", "UserManagement");

    public static string AccountsFilePath =>
        Path.Combine(ConfigDirectory, "Accounts.json");

    public static string UiPermissionsFilePath =>
        Path.Combine(ConfigDirectory, "UiPermissions.json");
}
