using Module.User.Features.AccountManagement.Models;

namespace Module.User.Features.AccountManagement.Services;

public sealed class AccountManagementService
{
    public AccountCatalog LoadCatalog()
    {
        return AccountConfigurationStore.LoadCatalog();
    }

    public void SaveCatalog(AccountCatalog catalog)
    {
        AccountConfigurationStore.SaveCatalog(catalog);
    }
}
