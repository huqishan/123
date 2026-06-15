using Module.User.Features.PermissionConfiguration.Models;

namespace Module.User.Features.PermissionConfiguration.Services;

public static class UiPermissionResolver
{
    public static UiPermissionResolvedSetting Resolve(
        UiPermissionCatalog catalog,
        string roleId,
        string key)
    {
        Dictionary<string, UiPermissionElementSetting> settings =
            UiPermissionConfigurationStore.GetRoleSettingMap(catalog, roleId);

        return settings.TryGetValue(key, out UiPermissionElementSetting? setting)
            ? new UiPermissionResolvedSetting(setting.IsVisible, setting.IsEnabled)
            : new UiPermissionResolvedSetting(false, false);
    }
}
