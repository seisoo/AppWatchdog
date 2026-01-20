using System;
using System.Security.Cryptography;
using System.Text;

namespace AppWatchdog.Shared;

public static class ConfigCrypto
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("AppWatchdog.Config.v1");

    public static string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain))
            return plain;

        var bytes = Encoding.UTF8.GetBytes(plain);

        var encrypted = ProtectedData.Protect(
            bytes,
            Entropy,
            DataProtectionScope.LocalMachine);

        return Convert.ToBase64String(encrypted);
    }

    public static string Decrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return encrypted;

        try
        {
            var bytes = Convert.FromBase64String(encrypted);

            var decrypted = ProtectedData.Unprotect(
                bytes,
                Entropy,
                DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return "";
        }
    }
}
