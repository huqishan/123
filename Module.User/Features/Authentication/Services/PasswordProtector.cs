using System;
using System.Security.Cryptography;
using System.Text;

namespace Module.User.Features.Authentication.Services;

public static class PasswordProtector
{
    private static readonly byte[] PasswordEntropy =
        Encoding.UTF8.GetBytes("WpfApp.UserManagement.AccountPassword.v1");

    public static string Protect(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                PasswordEntropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static bool Verify(string password, string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return false;
        }

        byte[]? plainBytes = null;
        byte[] inputBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(protectedPassword);
            plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                PasswordEntropy,
                DataProtectionScope.CurrentUser);

            return plainBytes.Length == inputBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(plainBytes, inputBytes);
        }
        catch
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputBytes);
            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }
}
