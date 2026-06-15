using Module.User.Features.AccountManagement.Models;

namespace Module.User.Features.AccountManagement.Services;

public static class AccountValidator
{
    public static bool IsReservedBuiltInAccount(string account)
    {
        return AccountConfigurationStore.IsReservedBuiltInAccount(account);
    }

    public static bool HasDuplicateAccount(
        AccountCatalog catalog,
        string account,
        string? ignoreAccountId = null)
    {
        return AccountConfigurationStore.HasDuplicateAccount(catalog, account, ignoreAccountId);
    }
}
