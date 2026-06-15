using ControlLibrary.Controls.Navigation.Models;
using Module.User.Features.PermissionConfiguration.Models;

namespace Module.User.Features.PermissionConfiguration.Services;

public sealed class UiPermissionConfigurationService
{
    public UiPermissionCatalog LoadCatalog()
    {
        return UiPermissionConfigurationStore.LoadCatalog();
    }

    public void SaveCatalog(UiPermissionCatalog catalog)
    {
        UiPermissionConfigurationStore.SaveCatalog(catalog);
    }

    public IReadOnlyList<UiPermissionNodeDefinition> Discover(
        IEnumerable<ControlInfoDataItem>? navigationItems = null)
    {
        return UiPermissionDiscoveryService.Discover(navigationItems);
    }
}
